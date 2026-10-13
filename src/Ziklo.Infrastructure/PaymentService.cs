using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ziklo.Application;
using Ziklo.Domain;
using Ziklo.FinancialEngine;

namespace Ziklo.Infrastructure;

public sealed class PaymentService(ZikloDbContext db) : IPaymentService
{
    public async Task<PaymentReceiptView?> GetAsync(Guid companyId, Guid loanId,
        Guid receiptId, CancellationToken ct)
    {
        var receipt = await db.PaymentReceipts.AsNoTracking().SingleOrDefaultAsync(x =>
            x.CompanyId == companyId && x.LoanId == loanId && x.Id == receiptId, ct);
        return receipt is null ? null : await View(receipt, ct);
    }

    public async Task<PaymentReceiptView> ReceiveAsync(Guid companyId, Guid loanId,
        ReceivePayment command, Guid actorUserId, CancellationToken ct)
    {
        string key = command.IdempotencyKey?.Trim() ?? string.Empty;
        string method = command.Method?.Trim() ?? string.Empty;
        string reference = command.ExternalReference?.Trim() ?? string.Empty;
        if (key.Length is < 1 or > 120 || method.Length is < 1 or > 40 ||
            reference.Length is < 1 or > 120 || command.Amount <= 0 || actorUserId == Guid.Empty)
            throw new LendingRuleException("El recaudo requiere monto, medio, referencia, usuario y clave de idempotencia válidos.");
        if (command.InstallmentNumbers is { } requested &&
            (requested.Count == 0 || requested.Any(number => number <= 0) ||
             requested.Distinct().Count() != requested.Count))
            throw new LendingRuleException("Selecciona una o varias cuotas válidas, sin repetirlas.");

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var existing = await db.PaymentReceipts.AsNoTracking().SingleOrDefaultAsync(x =>
            x.CompanyId == companyId && x.IdempotencyKey == key, ct);
        if (existing is not null)
        {
            if (existing.LoanId != loanId || existing.EffectiveDate != command.EffectiveDate ||
                existing.Received != command.Amount || existing.Method != method ||
                existing.ExternalReference != reference || existing.ActorUserId != actorUserId)
                throw new LendingRuleException("La clave de idempotencia ya identifica otro recaudo.");
            return await View(existing, ct);
        }

        var loan = await db.Loans.SingleOrDefaultAsync(x => x.CompanyId == companyId && x.Id == loanId, ct)
            ?? throw new LendingRuleException("El préstamo no existe en la empresa.");
        var company = await db.Companies.AsNoTracking().SingleAsync(x => x.Id == companyId, ct);
        if (command.EffectiveDate > DailyCloseService.BusinessDate(company))
            throw new LendingRuleException("No se puede registrar un recaudo con fecha futura.");
        if (loan.Status is not (LoanStatus.Active or LoanStatus.InArrears) ||
            command.EffectiveDate < loan.DisbursementDate)
            throw new LendingRuleException("El préstamo no está habilitado para recibir recaudos en esa fecha.");
        if (await db.PaymentReceipts.AnyAsync(x => x.CompanyId == companyId && x.LoanId == loanId &&
            x.EffectiveDate > command.EffectiveDate, ct))
            throw new LendingRuleException("No se admiten recaudos anteriores a uno ya registrado.");
        if (await db.DailyCloseRuns.AnyAsync(x => x.CompanyId == companyId &&
            x.BusinessDate >= command.EffectiveDate, ct))
            throw new LendingRuleException("La fecha del recaudo ya está cerrada.");
        var terms = await db.LoanTerms.AsNoTracking().SingleAsync(x =>
            x.CompanyId == companyId && x.LoanId == loanId, ct);
        if (Math.Round(command.Amount, terms.CurrencyDecimals) != command.Amount)
            throw new LendingRuleException("El monto supera los decimales de la moneda del préstamo.");
        var plan = await db.LoanPlans.AsNoTracking().SingleAsync(x =>
            x.CompanyId == companyId && x.LoanId == loanId && x.IsCurrent, ct);
        var due = await db.LoanInstallments.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.LoanId == loanId && x.PlanId == plan.Id &&
                x.DueDate <= command.EffectiveDate)
            .OrderBy(x => x.DueDate).ThenBy(x => x.Number).ToListAsync(ct);
        if (due.Count == 0)
            throw new LendingRuleException("Aún no hay cuotas vencidas o exigibles para aplicar el recaudo.");
        var selectedNumbers = command.InstallmentNumbers?.ToHashSet();
        if (selectedNumbers is not null &&
            selectedNumbers.Except(due.Select(line => line.Number)).Any())
            throw new LendingRuleException("Solo se pueden seleccionar cuotas exigibles en la fecha del recaudo.");

        var pastAllocations = await db.PaymentAllocations.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.LoanId == loanId)
            .ToListAsync(ct);
        var receiptDates = await db.PaymentReceipts.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.LoanId == loanId)
            .ToDictionaryAsync(x => x.Id, x => x.EffectiveDate, ct);
        var lateMovements = await db.LoanMovements.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.LoanId == loanId &&
                x.Component == BalanceComponent.LateInterest && x.Amount > 0)
            .ToListAsync(ct);
        var paid = pastAllocations.GroupBy(x => (x.InstallmentId, x.Component))
            .ToDictionary(x => x.Key, x => x.Sum(y => y.Amount));
        decimal Remaining(LoanInstallment line, BalanceComponent component, decimal planned)
        {
            decimal remainder = planned - paid.GetValueOrDefault((line.Id, component));
            if (remainder < 0) throw new LendingRuleException("Las aplicaciones anteriores exceden el plan.");
            return remainder;
        }

        var buckets = new List<PaymentBucket>();
        var componentByBucket = new Dictionary<Guid, BalanceComponent>();
        var lateProposals = new List<(LoanInstallment Line, LateInterestResult Result,
            decimal BaseAtCutoff)>();
        foreach (var line in due)
        {
            Add(line, BalanceComponent.Insurance, PaymentCategory.Charges, line.Insurance);
            Add(line, BalanceComponent.Charge, PaymentCategory.Charges, line.OtherCharges);
            decimal priorLate = lateMovements.Where(x => x.InstallmentId == line.Id)
                .Sum(x => x.Amount);
            decimal lateIncrement = 0;
            if (terms.LateAnnualRate > 0 && line.DueDate < command.EffectiveDate)
            {
                var principalPayments = pastAllocations
                    .Where(x => x.InstallmentId == line.Id && x.Component == BalanceComponent.Principal)
                    .Select(x => (Date: receiptDates[x.ReceiptId], x.Amount))
                    .GroupBy(x => x.Date).OrderBy(x => x.Key).ToList();
                decimal baseAmount = line.Principal;
                foreach (var group in principalPayments.Where(x => x.Key <= line.DueDate))
                    baseAmount -= group.Sum(x => x.Amount);
                var timeline = new List<LateBasePoint> { new(line.DueDate, baseAmount) };
                foreach (var group in principalPayments.Where(x => x.Key > line.DueDate &&
                             x.Key <= command.EffectiveDate))
                {
                    baseAmount -= group.Sum(x => x.Amount);
                    timeline.Add(new LateBasePoint(group.Key, baseAmount));
                }
                try
                {
                    var result = new LateInterestEngine().CalculateLateInterest(
                        line.DueDate, command.EffectiveDate, timeline,
                        new LateInterestRule(terms.LateAnnualRate, terms.LateGraceDays,
                            terms.LateGraceMode, terms.DayCount, terms.CurrencyDecimals,
                            terms.LateMaximumPerInstallment), priorLate);
                    lateIncrement = result.IncrementToPost;
                    if (lateIncrement > 0) lateProposals.Add((line, result, baseAmount));
                }
                catch (FinancialCalculationException error) { throw new LendingRuleException(error.Message); }
            }
            Add(line, BalanceComponent.LateInterest, PaymentCategory.LateInterest,
                priorLate + lateIncrement);
            Add(line, BalanceComponent.CurrentInterest,
                line.DueDate < command.EffectiveDate ? PaymentCategory.OverdueCurrentInterest : PaymentCategory.CurrentInterest,
                line.Interest - line.CapitalizedInterest);
            Add(line, BalanceComponent.Principal,
                line.DueDate < command.EffectiveDate ? PaymentCategory.OverduePrincipal : PaymentCategory.Principal,
                line.Principal);
        }
        void Add(LoanInstallment line, BalanceComponent component, PaymentCategory category, decimal planned)
        {
            decimal remaining = Remaining(line, component, planned);
            if (remaining == 0) return;
            Guid bucketId = Guid.NewGuid();
            buckets.Add(new PaymentBucket(bucketId, line.Id, category, line.DueDate, remaining));
            componentByBucket.Add(bucketId, component);
        }

        var eligibleBuckets = selectedNumbers is null ? buckets : buckets
            .Where(bucket => due.Any(line => line.Id == bucket.InstallmentId &&
                selectedNumbers.Contains(line.Number))).ToList();
        if (eligibleBuckets.Count == 0)
            throw new LendingRuleException("Las cuotas seleccionadas no tienen saldo pendiente.");

        PaymentApplication application;
        try
        {
            var priority = terms.PaymentPriority.Split(',')
                .Select(x => Enum.Parse<PaymentCategory>(x, ignoreCase: false)).ToList();
            var policy = PaymentEngine.DefaultPolicy with
            {
                Priority = priority,
                ExcessHandling = ExcessHandling.Reject,
                CurrencyDecimals = terms.CurrencyDecimals
            };
            application = new PaymentEngine().ApplyPayment(command.Amount, eligibleBuckets, policy);
        }
        catch (FinancialCalculationException error) { throw new LendingRuleException(error.Message); }
        catch (ArgumentException) { throw new LendingRuleException("La prioridad de pago del contrato es inválida."); }

        long sequence = await db.LoanMovements.Where(x => x.CompanyId == companyId && x.LoanId == loanId)
            .MaxAsync(x => (long?)x.Sequence, ct) ?? 0;
        var posted = await db.LoanEvents.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.LoanId == loanId &&
                x.Type == LoanEventType.CurrentInterestAccrual)
            .Select(x => x.IdempotencyKey).ToListAsync(ct);
        var postedKeys = posted.ToHashSet();
        foreach (var line in due)
        {
            string accrualKey = $"accrue:{line.Id:N}";
            if (postedKeys.Contains(accrualKey)) continue;
            if (line.Interest == 0 && line.Insurance == 0 && line.OtherCharges == 0) continue;
            var accrual = new LoanEvent
            {
                CompanyId = companyId, LoanId = loanId,
                Type = LoanEventType.CurrentInterestAccrual,
                EffectiveDate = line.DueDate, OriginReference = line.Id.ToString("N"),
                IdempotencyKey = accrualKey, ActorUserId = actorUserId
            };
            db.LoanEvents.Add(accrual);
            Post(accrual.Id, line.Id, BalanceComponent.CurrentInterest, line.Interest);
            Post(accrual.Id, line.Id, BalanceComponent.Insurance, line.Insurance);
            Post(accrual.Id, line.Id, BalanceComponent.Charge, line.OtherCharges);
        }

        var capitalizationKeys = await db.LoanEvents.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.LoanId == loanId &&
                x.Type == LoanEventType.Capitalization)
            .Select(x => x.IdempotencyKey).ToListAsync(ct);
        var capitalized = capitalizationKeys.ToHashSet();
        foreach (var line in due.Where(x => x.CapitalizedInterest > 0))
        {
            string capitalizationKey = $"capitalize:{line.Id:N}";
            if (capitalized.Contains(capitalizationKey)) continue;
            var evt = new LoanEvent
            {
                CompanyId = companyId, LoanId = loanId,
                Type = LoanEventType.Capitalization, EffectiveDate = line.DueDate,
                OriginReference = line.Id.ToString("N"),
                IdempotencyKey = capitalizationKey, ActorUserId = actorUserId
            };
            db.LoanEvents.Add(evt);
            Post(evt.Id, line.Id, BalanceComponent.CurrentInterest,
                -line.CapitalizedInterest);
            Post(evt.Id, line.Id, BalanceComponent.Principal,
                line.CapitalizedInterest);
        }

        foreach (var proposal in lateProposals)
        {
            var line = proposal.Line;
            string lateKey = $"late:{line.Id:N}:{command.EffectiveDate:yyyyMMdd}";
            var evt = new LoanEvent
            {
                CompanyId = companyId, LoanId = loanId,
                Type = LoanEventType.LateInterestAccrual,
                EffectiveDate = command.EffectiveDate,
                OriginReference = line.Id.ToString("N"),
                IdempotencyKey = lateKey, ActorUserId = actorUserId
            };
            db.LoanEvents.Add(evt);
            Post(evt.Id, line.Id, BalanceComponent.LateInterest,
                proposal.Result.IncrementToPost);
            db.LateInterestAssessments.Add(new LateInterestAssessment
            {
                CompanyId = companyId, LoanId = loanId, InstallmentId = line.Id,
                EventId = evt.Id, DueDate = line.DueDate,
                CutoffDate = command.EffectiveDate,
                EligibleDays = proposal.Result.EligibleDays,
                BaseAtCutoff = proposal.BaseAtCutoff,
                AnnualRate = terms.LateAnnualRate,
                AlreadyPosted = proposal.Result.AlreadyPosted,
                CumulativeInterest = proposal.Result.CumulativeInterest,
                AmountPosted = proposal.Result.IncrementToPost,
                GraceDays = terms.LateGraceDays,
                GraceMode = terms.LateGraceMode, DayCount = terms.DayCount
            });
        }

        var paymentEvent = new LoanEvent
        {
            CompanyId = companyId, LoanId = loanId, Type = LoanEventType.Payment,
            EffectiveDate = command.EffectiveDate, OriginReference = reference,
            IdempotencyKey = key, ActorUserId = actorUserId
        };
        db.LoanEvents.Add(paymentEvent);
        var receipt = new PaymentReceipt
        {
            CompanyId = companyId, LoanId = loanId, ClientId = loan.ClientId,
            EventId = paymentEvent.Id, EffectiveDate = command.EffectiveDate,
            Received = command.Amount, Applied = application.Applied, Credit = 0,
            CurrencyCode = loan.CurrencyCode, Method = method,
            ExternalReference = reference, IdempotencyKey = key, ActorUserId = actorUserId
        };
        db.PaymentReceipts.Add(receipt);
        db.OutboxMessages.Add(new OutboxMessage
        {
            CompanyId = companyId, EventType = "PaymentReceived", AggregateId = receipt.Id,
            PayloadJson = JsonSerializer.Serialize(new
            {
                companyId, loanId, receiptId = receipt.Id,
                eventId = paymentEvent.Id, receipt.EffectiveDate,
                receipt.Received, receipt.CurrencyCode,
                receipt.Method, receipt.ExternalReference
            })
        });
        int order = 0;
        foreach (var allocation in application.Allocations)
        {
            var component = componentByBucket[allocation.BucketId];
            var installmentId = allocation.InstallmentId!.Value;
            db.PaymentAllocations.Add(new PaymentAllocationRecord
            {
                CompanyId = companyId, LoanId = loanId, ReceiptId = receipt.Id,
                InstallmentId = installmentId, Component = component,
                Amount = allocation.Amount, Order = ++order
            });
            Post(paymentEvent.Id, installmentId, component, -allocation.Amount);
        }
        void Post(Guid eventId, Guid installmentId, BalanceComponent component, decimal amount)
        {
            if (amount == 0) return;
            db.LoanMovements.Add(new LoanMovement
            {
                CompanyId = companyId, LoanId = loanId, EventId = eventId,
                InstallmentId = installmentId, Sequence = ++sequence,
                Component = component, Amount = amount, CurrencyCode = loan.CurrencyCode
            });
        }

        int planInstallments = await db.LoanInstallments.CountAsync(x =>
            x.CompanyId == companyId && x.LoanId == loanId && x.PlanId == plan.Id, ct);
        var appliedByBucket = application.Allocations.ToDictionary(x => x.BucketId, x => x.Amount);
        var allRemaining = buckets.Select(bucket => bucket with
        {
            Outstanding = bucket.Outstanding - appliedByBucket.GetValueOrDefault(bucket.BucketId)
        }).ToList();
        if (due.Count == planInstallments && allRemaining.All(x => x.Outstanding == 0))
            loan.Status = LoanStatus.PaidOff;
        else
        {
            LoanStatus nextStatus = allRemaining.Any(x =>
                x.DueDate < command.EffectiveDate && x.Outstanding > 0)
                ? LoanStatus.InArrears : LoanStatus.Active;
            if (loan.Status == nextStatus)
                db.Entry(loan).Property(x => x.Status).IsModified = true;
            else
                loan.Status = nextStatus;
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await View(receipt, ct);
    }

    private async Task<PaymentReceiptView> View(PaymentReceipt receipt, CancellationToken ct)
    {
        var allocations = await db.PaymentAllocations.AsNoTracking()
            .Where(x => x.CompanyId == receipt.CompanyId && x.LoanId == receipt.LoanId &&
                x.ReceiptId == receipt.Id)
            .OrderBy(x => x.Order)
            .Select(x => new PaymentAllocationView(x.InstallmentId, x.Component, x.Amount, x.Order))
            .ToListAsync(ct);
        return new PaymentReceiptView(receipt.Id, receipt.LoanId, receipt.EffectiveDate,
            receipt.Received, receipt.Applied, receipt.Credit, receipt.CurrencyCode,
            receipt.Method, receipt.ExternalReference, receipt.IdempotencyKey, allocations);
    }
}
