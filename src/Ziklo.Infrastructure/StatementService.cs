using Microsoft.EntityFrameworkCore;
using Ziklo.Application;
using Ziklo.Domain;

namespace Ziklo.Infrastructure;

public sealed class StatementService(ZikloDbContext db, IPortfolioService portfolioService)
    : IStatementService
{
    public async Task<LoanStatement?> GetAsync(Guid companyId, Guid loanId,
        DateOnly asOf, CancellationToken ct)
    {
        var loan = await db.Loans.AsNoTracking().SingleOrDefaultAsync(x =>
            x.CompanyId == companyId && x.Id == loanId, ct);
        if (loan is null) return null;
        PortfolioView portfolio = await portfolioService.GetAsync(companyId, asOf, ct);
        var balance = portfolio.Loans.SingleOrDefault(x => x.LoanId == loanId);
        var events = await db.LoanEvents.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.LoanId == loanId &&
                x.EffectiveDate <= asOf).ToListAsync(ct);
        var byId = events.ToDictionary(x => x.Id);
        var movements = await db.LoanMovements.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.LoanId == loanId).ToListAsync(ct);
        var visibleMovements = movements.Where(x => byId.ContainsKey(x.EventId)).ToList();
        decimal Paid(BalanceComponent component) => -visibleMovements.Where(x =>
            x.Component == component && x.Amount < 0 &&
            byId[x.EventId].Type == LoanEventType.Payment).Sum(x => x.Amount);
        var receipts = await db.PaymentReceipts.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.LoanId == loanId &&
                x.EffectiveDate <= asOf)
            .OrderBy(x => x.EffectiveDate).ThenBy(x => x.CreatedAtUtc).ToListAsync(ct);
        var receiptIds = receipts.Select(x => x.Id).ToHashSet();
        var allocations = await db.PaymentAllocations.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.LoanId == loanId).ToListAsync(ct);
        var paidByLine = allocations.Where(x => receiptIds.Contains(x.ReceiptId))
            .GroupBy(x => (x.InstallmentId, x.Component))
            .ToDictionary(x => x.Key, x => x.Sum(y => y.Amount));
        var plan = await db.LoanPlans.AsNoTracking().SingleOrDefaultAsync(x =>
            x.CompanyId == companyId && x.LoanId == loanId && x.IsCurrent, ct);
        var lines = plan is null ? [] : await db.LoanInstallments.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.LoanId == loanId && x.PlanId == plan.Id)
            .OrderBy(x => x.DueDate).ToListAsync(ct);
        decimal Remaining(LoanInstallment line, BalanceComponent component, decimal planned) =>
            planned - paidByLine.GetValueOrDefault((line.Id, component));
        bool IsOutstanding(LoanInstallment line)
        {
            decimal due = Remaining(line, BalanceComponent.Principal, line.Principal) +
                Remaining(line, BalanceComponent.CurrentInterest,
                    line.Interest - line.CapitalizedInterest) +
                Remaining(line, BalanceComponent.Insurance, line.Insurance) +
                Remaining(line, BalanceComponent.Charge, line.OtherCharges) +
                visibleMovements.Where(x => x.InstallmentId == line.Id &&
                    x.Component == BalanceComponent.LateInterest).Sum(x => x.Amount);
            if (due < 0) throw new LendingRuleException("La cuota tiene aplicaciones superiores a su deuda.");
            return due > 0;
        }
        var pastDue = lines.Where(x => x.DueDate < asOf && IsOutstanding(x)).ToList();
        DateOnly? nextDue = lines.Where(x => x.DueDate > asOf && IsOutstanding(x))
            .Select(x => (DateOnly?)x.DueDate).FirstOrDefault();
        var entries = visibleMovements.OrderBy(x => byId[x.EventId].EffectiveDate)
            .ThenBy(x => x.Sequence)
            .Select(x => new StatementMovement(x.Sequence, byId[x.EventId].EffectiveDate,
                byId[x.EventId].Type, x.Component, x.Amount, x.InstallmentId)).ToList();
        var receiptViews = receipts.Select(x => new StatementReceipt(x.Id, x.EffectiveDate,
            x.Received, x.Method, x.ExternalReference)).ToList();
        return new LoanStatement(loan.Id, loan.ClientId, loan.Number, asOf,
            loan.OriginalPrincipal, balance?.Principal ?? 0,
            balance?.CurrentInterest ?? 0, balance?.LateInterest ?? 0,
            balance?.Insurance ?? 0, balance?.Charges ?? 0, balance?.Credit ?? 0,
            balance?.Total ?? 0, Paid(BalanceComponent.Principal),
            Paid(BalanceComponent.CurrentInterest), Paid(BalanceComponent.LateInterest),
            nextDue, pastDue.Count, balance?.DaysPastDue ?? 0,
            receiptViews, entries);
    }
}
