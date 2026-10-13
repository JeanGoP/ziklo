using Microsoft.EntityFrameworkCore;
using Ziklo.Application;
using Ziklo.Domain;

namespace Ziklo.Infrastructure;

public sealed class PortfolioService(ZikloDbContext db) : IPortfolioService
{
    public async Task<ClientPortfolioSummary?> GetClientAsync(Guid companyId,
        Guid clientId, DateOnly asOf, CancellationToken ct)
    {
        var client = await db.Clients.AsNoTracking().SingleOrDefaultAsync(x =>
            x.CompanyId == companyId && x.Id == clientId, ct);
        if (client is null) return null;
        PortfolioView portfolio = await GetAsync(companyId, asOf, ct);
        var rows = portfolio.Loans.Where(x => x.ClientId == clientId).ToList();
        int disbursed = await db.Loans.CountAsync(x => x.CompanyId == companyId &&
            x.ClientId == clientId && x.DisbursementDate <= asOf, ct);
        var receipts = await db.PaymentReceipts.AsNoTracking().Where(x =>
            x.CompanyId == companyId && x.ClientId == clientId && x.EffectiveDate <= asOf)
            .Select(x => x.Received).ToListAsync(ct);
        return new ClientPortfolioSummary(client.Id, client.Name,
            client.IdentificationType, client.IdentificationNumber, asOf,
            rows.Count, disbursed - rows.Count,
            rows.Count(x => x.DaysPastDue > 0),
            rows.Sum(x => x.Principal), rows.Sum(x => x.CurrentInterest),
            rows.Sum(x => x.LateInterest), rows.Sum(x => x.Total),
            receipts.Count, receipts.Sum(), rows);
    }

    public async Task<PortfolioView> GetAsync(Guid companyId, DateOnly asOf, CancellationToken ct)
    {
        var company = await db.Companies.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == companyId && x.IsActive, ct)
            ?? throw new LendingRuleException("La empresa no existe o está inactiva.");
        if (asOf == default || asOf > DailyCloseService.BusinessDate(company))
            throw new LendingRuleException("La fecha de corte no es válida.");
        var loans = await db.Loans.AsNoTracking().Where(x => x.CompanyId == companyId &&
            x.DisbursementDate <= asOf).OrderBy(x => x.Number).ToListAsync(ct);
        var events = await db.LoanEvents.AsNoTracking().Where(x =>
            x.CompanyId == companyId && x.EffectiveDate <= asOf)
            .Select(x => x.Id).ToListAsync(ct);
        var eventIds = events.ToHashSet();
        var movements = await db.LoanMovements.AsNoTracking()
            .Where(x => x.CompanyId == companyId).ToListAsync(ct);
        var movementsByLoan = movements.Where(x => eventIds.Contains(x.EventId))
            .GroupBy(x => x.LoanId).ToDictionary(x => x.Key, x => x.ToList());
        var plans = await db.LoanPlans.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.IsCurrent).ToListAsync(ct);
        var planIds = plans.Select(x => x.Id).ToHashSet();
        var installments = await db.LoanInstallments.AsNoTracking()
            .Where(x => x.CompanyId == companyId).ToListAsync(ct);
        var installmentsByLoan = installments.Where(x => planIds.Contains(x.PlanId))
            .GroupBy(x => x.LoanId).ToDictionary(x => x.Key, x => x.ToList());
        var receipts = await db.PaymentReceipts.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.EffectiveDate <= asOf)
            .Select(x => x.Id).ToListAsync(ct);
        var receiptIds = receipts.ToHashSet();
        var allocations = await db.PaymentAllocations.AsNoTracking()
            .Where(x => x.CompanyId == companyId).ToListAsync(ct);
        var paid = allocations.Where(x => receiptIds.Contains(x.ReceiptId))
            .GroupBy(x => (x.InstallmentId, x.Component))
            .ToDictionary(x => x.Key, x => x.Sum(y => y.Amount));
        var rows = new List<PortfolioLoanView>();
        foreach (var loan in loans)
        {
            movementsByLoan.TryGetValue(loan.Id, out var entries);
            entries ??= [];
            decimal Balance(BalanceComponent component) => entries.Where(x => x.Component == component)
                .Sum(x => x.Amount);
            decimal principal = Balance(BalanceComponent.Principal);
            decimal current = Balance(BalanceComponent.CurrentInterest);
            decimal late = Balance(BalanceComponent.LateInterest);
            decimal insurance = Balance(BalanceComponent.Insurance);
            decimal charges = Balance(BalanceComponent.Charge);
            decimal credit = Balance(BalanceComponent.Credit);
            decimal total = principal + current + late + insurance + charges - credit;
            if (principal < 0 || current < 0 || late < 0 || insurance < 0 ||
                charges < 0 || credit < 0 || total < 0)
                throw new LendingRuleException("El libro contiene un saldo financiero inconsistente.");
            if (total == 0) continue;
            int daysPastDue = 0;
            if (installmentsByLoan.TryGetValue(loan.Id, out var lines))
            {
                foreach (var line in lines.Where(x => x.DueDate < asOf))
                {
                    decimal Remaining(BalanceComponent component, decimal planned) =>
                        planned - paid.GetValueOrDefault((line.Id, component));
                    decimal principalDue = Remaining(BalanceComponent.Principal, line.Principal);
                    decimal currentDue = Remaining(BalanceComponent.CurrentInterest,
                        line.Interest - line.CapitalizedInterest);
                    decimal insuranceDue = Remaining(BalanceComponent.Insurance, line.Insurance);
                    decimal chargesDue = Remaining(BalanceComponent.Charge, line.OtherCharges);
                    if (principalDue < 0 || currentDue < 0 || insuranceDue < 0 || chargesDue < 0)
                        throw new LendingRuleException("Las aplicaciones exceden una cuota del plan.");
                    decimal lateDue = entries.Where(x => x.InstallmentId == line.Id &&
                        x.Component == BalanceComponent.LateInterest).Sum(x => x.Amount);
                    if (lateDue < 0)
                        throw new LendingRuleException("La mora aplicada excede la causación de una cuota.");
                    if (principalDue + currentDue + insuranceDue + chargesDue + lateDue > 0)
                        daysPastDue = Math.Max(daysPastDue, asOf.DayNumber - line.DueDate.DayNumber);
                }
            }
            rows.Add(new PortfolioLoanView(loan.Id, loan.ClientId, loan.Number,
                loan.CurrencyCode, daysPastDue, Aging(daysPastDue), principal,
                current, late, insurance, charges, credit, total));
        }
        return new PortfolioView(asOf, rows.Count, rows.Count(x => x.DaysPastDue > 0),
            rows.Sum(x => x.Principal), rows.Sum(x => x.CurrentInterest),
            rows.Sum(x => x.LateInterest), rows.Sum(x => x.Insurance),
            rows.Sum(x => x.Charges), rows.Sum(x => x.Credit), rows.Sum(x => x.Total), rows);
    }

    private static string Aging(int days) => days switch
    {
        <= 0 => "Por vencer",
        <= 30 => "1-30",
        <= 60 => "31-60",
        <= 90 => "61-90",
        <= 120 => "91-120",
        <= 150 => "121-150",
        <= 180 => "151-180",
        _ => "Más de 180"
    };
}
