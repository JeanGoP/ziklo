using Microsoft.EntityFrameworkCore;
using Ziklo.Application;
using Ziklo.Domain;

namespace Ziklo.Infrastructure;

public sealed class DashboardService(ZikloDbContext db, IPortfolioService portfolioService)
    : IDashboardService
{
    public async Task<DashboardView> GetAsync(Guid companyId, DateOnly asOf, CancellationToken ct)
    {
        PortfolioView portfolio = await portfolioService.GetAsync(companyId, asOf, ct);
        var loans = await db.Loans.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.DisbursementDate <= asOf)
            .Select(x => new { x.Id, x.OriginalPrincipal }).ToListAsync(ct);
        var receipts = await db.PaymentReceipts.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.EffectiveDate <= asOf)
            .Select(x => new { x.EffectiveDate, x.Received }).ToListAsync(ct);
        var events = await db.LoanEvents.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.EffectiveDate <= asOf)
            .Select(x => new { x.Id, x.Type }).ToListAsync(ct);
        var eventTypes = events.ToDictionary(x => x.Id, x => x.Type);
        var movements = await db.LoanMovements.AsNoTracking()
            .Where(x => x.CompanyId == companyId &&
                (x.Component == BalanceComponent.CurrentInterest ||
                 x.Component == BalanceComponent.LateInterest))
            .Select(x => new { x.EventId, x.Component, x.Amount }).ToListAsync(ct);
        decimal Flow(BalanceComponent component, bool collected) => movements
            .Where(x => x.Component == component && eventTypes.TryGetValue(x.EventId, out var type) &&
                (collected ? x.Amount < 0 && type == LoanEventType.Payment : x.Amount > 0))
            .Sum(x => collected ? -x.Amount : x.Amount);
        DateOnly monthStart = new(asOf.Year, asOf.Month, 1);
        string[] buckets = ["Por vencer", "1-30", "31-60", "61-90", "91-120",
            "121-150", "151-180", "Más de 180"];
        var aging = buckets.Select(bucket =>
        {
            var rows = portfolio.Loans.Where(x => x.AgingBucket == bucket).ToList();
            return new AgingSummary(bucket, rows.Count, rows.Sum(x => x.Total));
        }).ToList();
        return new DashboardView(asOf, loans.Sum(x => x.OriginalPrincipal),
            portfolio.Total, portfolio.Principal, portfolio.CurrentInterest,
            portfolio.LateInterest,
            receipts.Where(x => x.EffectiveDate == asOf).Sum(x => x.Received),
            receipts.Where(x => x.EffectiveDate >= monthStart).Sum(x => x.Received),
            Flow(BalanceComponent.CurrentInterest, false),
            Flow(BalanceComponent.CurrentInterest, true),
            Flow(BalanceComponent.LateInterest, false),
            Flow(BalanceComponent.LateInterest, true),
            portfolio.LoanCount, portfolio.DelinquentLoanCount,
            loans.Count - portfolio.LoanCount, aging);
    }
}
