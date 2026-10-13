using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ziklo.Application;
using Ziklo.Domain;
using Ziklo.FinancialEngine;

namespace Ziklo.Infrastructure;

public sealed class DailyCloseService(ZikloDbContext db) : IDailyCloseService
{
    public async Task<DailyCloseView> RunAsync(Guid companyId, RunDailyClose command,
        Guid actorUserId, CancellationToken ct)
    {
        if (actorUserId == Guid.Empty || command.BusinessDate == default)
            throw new LendingRuleException("El cierre requiere usuario y fecha de negocio válidos.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var previous = await db.DailyCloseRuns.AsNoTracking().SingleOrDefaultAsync(x =>
            x.CompanyId == companyId && x.BusinessDate == command.BusinessDate, ct);
        if (previous is not null) return ToView(previous);
        var company = await db.Companies.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == companyId && x.IsActive, ct)
            ?? throw new LendingRuleException("La empresa no existe o está inactiva.");
        DateOnly today = BusinessDate(company);
        if (command.BusinessDate > today)
            throw new LendingRuleException("No se puede cerrar una fecha futura.");
        if (await db.DailyCloseRuns.AnyAsync(x => x.CompanyId == companyId &&
            x.BusinessDate > command.BusinessDate, ct))
            throw new LendingRuleException("No se puede cerrar una fecha anterior a un cierre existente.");
        if (await db.PaymentReceipts.AnyAsync(x => x.CompanyId == companyId &&
            x.EffectiveDate > command.BusinessDate, ct))
            throw new LendingRuleException("Hay recaudos posteriores a la fecha de cierre.");

        var loans = await db.Loans.Where(x => x.CompanyId == companyId &&
            (x.Status == LoanStatus.Active || x.Status == LoanStatus.InArrears) &&
            x.DisbursementDate <= command.BusinessDate).ToListAsync(ct);
        int accrualEvents = 0;
        int lateEvents = 0;
        foreach (var loan in loans)
        {
            var terms = await db.LoanTerms.AsNoTracking().SingleAsync(x =>
                x.CompanyId == companyId && x.LoanId == loan.Id, ct);
            var plan = await db.LoanPlans.AsNoTracking().SingleAsync(x =>
                x.CompanyId == companyId && x.LoanId == loan.Id && x.IsCurrent, ct);
            var due = await db.LoanInstallments.AsNoTracking()
                .Where(x => x.CompanyId == companyId && x.LoanId == loan.Id && x.PlanId == plan.Id &&
                    x.DueDate <= command.BusinessDate)
                .OrderBy(x => x.DueDate).ThenBy(x => x.Number).ToListAsync(ct);
            var receipts = await db.PaymentReceipts.AsNoTracking()
                .Where(x => x.CompanyId == companyId && x.LoanId == loan.Id)
                .ToDictionaryAsync(x => x.Id, x => x.EffectiveDate, ct);
            var allocations = await db.PaymentAllocations.AsNoTracking()
                .Where(x => x.CompanyId == companyId && x.LoanId == loan.Id).ToListAsync(ct);
            var events = await db.LoanEvents.AsNoTracking().Where(x =>
                x.CompanyId == companyId && x.LoanId == loan.Id)
                .Select(x => x.IdempotencyKey).ToListAsync(ct);
            var eventKeys = events.ToHashSet();
            var pastMovements = await db.LoanMovements.AsNoTracking()
                .Where(x => x.CompanyId == companyId && x.LoanId == loan.Id).ToListAsync(ct);
            long sequence = pastMovements.Count == 0 ? 0 : pastMovements.Max(x => x.Sequence);
            bool overdue = false;
            foreach (var line in due)
            {
                string accrualKey = $"accrue:{line.Id:N}";
                if (!eventKeys.Contains(accrualKey) &&
                    (line.Interest > 0 || line.Insurance > 0 || line.OtherCharges > 0))
                {
                    var accrual = NewEvent(loan, line, LoanEventType.CurrentInterestAccrual,
                        accrualKey, line.DueDate, actorUserId);
                    db.LoanEvents.Add(accrual);
                    Post(accrual, line, BalanceComponent.CurrentInterest, line.Interest);
                    Post(accrual, line, BalanceComponent.Insurance, line.Insurance);
                    Post(accrual, line, BalanceComponent.Charge, line.OtherCharges);
                    eventKeys.Add(accrualKey);
                    accrualEvents++;
                }
                if (line.CapitalizedInterest > 0)
                {
                    string capitalizationKey = $"capitalize:{line.Id:N}";
                    if (!eventKeys.Contains(capitalizationKey))
                    {
                        var capitalization = NewEvent(loan, line, LoanEventType.Capitalization,
                            capitalizationKey, line.DueDate, actorUserId);
                        db.LoanEvents.Add(capitalization);
                        Post(capitalization, line, BalanceComponent.CurrentInterest,
                            -line.CapitalizedInterest);
                        Post(capitalization, line, BalanceComponent.Principal,
                            line.CapitalizedInterest);
                        eventKeys.Add(capitalizationKey);
                    }
                }

                var lineAllocations = allocations.Where(x => x.InstallmentId == line.Id).ToList();
                decimal Paid(BalanceComponent component) => lineAllocations
                    .Where(x => x.Component == component).Sum(x => x.Amount);
                decimal principalLeft = line.Principal - Paid(BalanceComponent.Principal);
                decimal interestLeft = line.Interest - line.CapitalizedInterest -
                    Paid(BalanceComponent.CurrentInterest);
                decimal insuranceLeft = line.Insurance - Paid(BalanceComponent.Insurance);
                decimal chargesLeft = line.OtherCharges - Paid(BalanceComponent.Charge);
                if (principalLeft < 0 || interestLeft < 0 || insuranceLeft < 0 || chargesLeft < 0)
                    throw new LendingRuleException("Las aplicaciones exceden la cuota contractual.");

                decimal priorLate = pastMovements.Where(x => x.InstallmentId == line.Id &&
                    x.Component == BalanceComponent.LateInterest && x.Amount > 0).Sum(x => x.Amount);
                decimal latePaid = Paid(BalanceComponent.LateInterest);
                if (latePaid > priorLate)
                    throw new LendingRuleException("Los pagos de mora exceden la mora causada.");
                if (terms.LateAnnualRate > 0 && line.DueDate < command.BusinessDate)
                {
                    var principalPayments = lineAllocations
                        .Where(x => x.Component == BalanceComponent.Principal)
                        .Select(x => (Date: receipts[x.ReceiptId], x.Amount))
                        .GroupBy(x => x.Date).OrderBy(x => x.Key).ToList();
                    decimal baseAmount = line.Principal;
                    foreach (var group in principalPayments.Where(x => x.Key <= line.DueDate))
                        baseAmount -= group.Sum(x => x.Amount);
                    var timeline = new List<LateBasePoint> { new(line.DueDate, baseAmount) };
                    foreach (var group in principalPayments.Where(x => x.Key > line.DueDate &&
                                 x.Key <= command.BusinessDate))
                    {
                        baseAmount -= group.Sum(x => x.Amount);
                        timeline.Add(new LateBasePoint(group.Key, baseAmount));
                    }
                    LateInterestResult result;
                    try
                    {
                        result = new LateInterestEngine().CalculateLateInterest(
                            line.DueDate, command.BusinessDate, timeline,
                            new LateInterestRule(terms.LateAnnualRate, terms.LateGraceDays,
                                terms.LateGraceMode, terms.DayCount, terms.CurrencyDecimals,
                                terms.LateMaximumPerInstallment), priorLate);
                    }
                    catch (FinancialCalculationException error) { throw new LendingRuleException(error.Message); }
                    if (result.IncrementToPost > 0)
                    {
                        string lateKey = $"late:{line.Id:N}:{command.BusinessDate:yyyyMMdd}";
                        if (!eventKeys.Contains(lateKey))
                        {
                            var lateEvent = NewEvent(loan, line, LoanEventType.LateInterestAccrual,
                                lateKey, command.BusinessDate, actorUserId);
                            db.LoanEvents.Add(lateEvent);
                            Post(lateEvent, line, BalanceComponent.LateInterest, result.IncrementToPost);
                            db.LateInterestAssessments.Add(new LateInterestAssessment
                            {
                                CompanyId = companyId, LoanId = loan.Id, InstallmentId = line.Id,
                                EventId = lateEvent.Id, DueDate = line.DueDate,
                                CutoffDate = command.BusinessDate, EligibleDays = result.EligibleDays,
                                BaseAtCutoff = baseAmount, AnnualRate = terms.LateAnnualRate,
                                AlreadyPosted = priorLate, CumulativeInterest = result.CumulativeInterest,
                                AmountPosted = result.IncrementToPost, GraceDays = terms.LateGraceDays,
                                GraceMode = terms.LateGraceMode, DayCount = terms.DayCount
                            });
                            priorLate += result.IncrementToPost;
                            eventKeys.Add(lateKey);
                            lateEvents++;
                        }
                    }
                }
                if (line.DueDate < command.BusinessDate &&
                    principalLeft + interestLeft + insuranceLeft + chargesLeft + priorLate - latePaid > 0)
                    overdue = true;
            }
            LoanStatus nextStatus = overdue ? LoanStatus.InArrears : LoanStatus.Active;
            if (loan.Status != nextStatus) loan.Status = nextStatus;

            void Post(LoanEvent evt, LoanInstallment line, BalanceComponent component, decimal amount)
            {
                if (amount == 0) return;
                db.LoanMovements.Add(new LoanMovement
                {
                    CompanyId = companyId, LoanId = loan.Id, EventId = evt.Id,
                    InstallmentId = line.Id, Sequence = ++sequence,
                    Component = component, Amount = amount, CurrencyCode = loan.CurrencyCode
                });
            }
        }
        var run = new DailyCloseRun
        {
            CompanyId = companyId, BusinessDate = command.BusinessDate,
            ActorUserId = actorUserId, LoansProcessed = loans.Count,
            AccrualEvents = accrualEvents, LateEvents = lateEvents
        };
        db.DailyCloseRuns.Add(run);
        var promises = await db.PaymentPromises.Where(x => x.CompanyId == companyId &&
            x.Status == PromiseStatus.Pending && x.DueDate <= command.BusinessDate)
            .ToListAsync(ct);
        var candidateReceipts = await db.PaymentReceipts.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.EffectiveDate <= command.BusinessDate)
            .ToListAsync(ct);
        foreach (var promise in promises)
        {
            decimal received = candidateReceipts.Where(x => x.LoanId == promise.LoanId &&
                x.EffectiveDate >= promise.StartDate && x.EffectiveDate <= promise.DueDate &&
                x.CreatedAtUtc >= promise.CreatedAtUtc)
                .Sum(x => x.Received);
            PromiseStatus status = received >= promise.PromisedAmount
                ? PromiseStatus.Fulfilled
                : received > 0 ? PromiseStatus.Partial : PromiseStatus.Broken;
            promise.Status = status;
            promise.EvaluatedOn = command.BusinessDate;
            promise.ReceivedAmount = received;
            db.PromiseEvaluations.Add(new PromiseEvaluation
            {
                CompanyId = companyId, LoanId = promise.LoanId,
                PromiseId = promise.Id, DailyCloseRunId = run.Id,
                Status = status, ReceivedAmount = received, ActorUserId = actorUserId
            });
        }
        run.PromisesEvaluated = promises.Count;
        db.OutboxMessages.Add(new OutboxMessage
        {
            CompanyId = companyId, EventType = "DailyCloseCompleted", AggregateId = run.Id,
            PayloadJson = JsonSerializer.Serialize(new
            {
                companyId, dailyCloseRunId = run.Id, run.BusinessDate,
                run.LoansProcessed, run.AccrualEvents, run.LateEvents,
                run.PromisesEvaluated
            })
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ToView(run);
    }

    private static LoanEvent NewEvent(Loan loan, LoanInstallment line,
        LoanEventType type, string key, DateOnly date, Guid actorUserId) => new()
    {
        CompanyId = loan.CompanyId, LoanId = loan.Id, Type = type,
        EffectiveDate = date, OriginReference = line.Id.ToString("N"),
        IdempotencyKey = key, ActorUserId = actorUserId
    };

    private static DailyCloseView ToView(DailyCloseRun run) => new(run.Id,
        run.BusinessDate, run.CompletedAtUtc, run.LoansProcessed,
        run.AccrualEvents, run.LateEvents, run.PromisesEvaluated);

    public static DateOnly BusinessDate(Company company)
    {
        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(company.TimeZoneId);
            return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone));
        }
        catch (TimeZoneNotFoundException)
        {
            throw new LendingRuleException("La zona horaria de la empresa no está disponible.");
        }
        catch (InvalidTimeZoneException)
        {
            throw new LendingRuleException("La zona horaria de la empresa no es válida.");
        }
    }
}
