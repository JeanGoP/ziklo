using Microsoft.EntityFrameworkCore;
using Ziklo.Application;
using Ziklo.Domain;
using Ziklo.FinancialEngine;

namespace Ziklo.Infrastructure;

public sealed class ConsistencyService(ZikloDbContext db) : IConsistencyService
{
    public async Task<ConsistencyReport> CheckAsync(Guid companyId, CancellationToken ct)
    {
        if (!await db.Companies.AnyAsync(x => x.Id == companyId && x.IsActive, ct))
            throw new LendingRuleException("La empresa no existe o está inactiva.");
        var loans = await db.Loans.AsNoTracking().Where(x => x.CompanyId == companyId)
            .ToListAsync(ct);
        var movements = await db.LoanMovements.AsNoTracking()
            .Where(x => x.CompanyId == companyId).ToListAsync(ct);
        var receipts = await db.PaymentReceipts.AsNoTracking()
            .Where(x => x.CompanyId == companyId).ToListAsync(ct);
        var allocations = await db.PaymentAllocations.AsNoTracking()
            .Where(x => x.CompanyId == companyId).ToListAsync(ct);
        var plans = await db.LoanPlans.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.IsCurrent).ToListAsync(ct);
        var installments = await db.LoanInstallments.AsNoTracking()
            .Where(x => x.CompanyId == companyId).ToListAsync(ct);
        var terms = await db.LoanTerms.AsNoTracking()
            .Where(x => x.CompanyId == companyId).ToListAsync(ct);
        var issues = new List<ConsistencyIssue>();
        foreach (var loan in loans)
        {
            var ledger = movements.Where(x => x.LoanId == loan.Id).ToList();
            OutstandingBalance? balance = null;
            try
            {
                balance = new BalanceEngine().CalculateOutstandingBalance(ledger.Select(x =>
                    new FinancialDelta(x.Sequence, Map(x.Component), x.Amount)).ToList());
            }
            catch (FinancialCalculationException error)
            {
                issues.Add(new ConsistencyIssue("LEDGER", loan.Id, error.Message));
            }
            if (loan.DisbursementDate.HasValue && ledger.Count == 0)
                issues.Add(new ConsistencyIssue("DISBURSEMENT", loan.Id,
                    "El préstamo desembolsado no tiene movimientos."));
            if (loan.Status == LoanStatus.PaidOff && balance is not null && balance.NetBalance != 0)
                issues.Add(new ConsistencyIssue("PAID_OFF_BALANCE", loan.Id,
                    "El préstamo cancelado conserva saldo en el libro."));
            var currentPlans = plans.Where(x => x.LoanId == loan.Id).ToList();
            if (loan.DisbursementDate.HasValue && currentPlans.Count != 1)
                issues.Add(new ConsistencyIssue("CURRENT_PLAN", loan.Id,
                    "El préstamo desembolsado debe tener un plan vigente."));
            if (currentPlans.Count == 1 &&
                terms.SingleOrDefault(x => x.LoanId == loan.Id) is { } loanTerms)
            {
                var lines = installments.Where(x => x.LoanId == loan.Id &&
                    x.PlanId == currentPlans[0].Id).ToList();
                decimal plannedPrincipal = lines.Sum(x => x.Principal);
                decimal expectedPrincipal = loan.OriginalPrincipal +
                    (loanTerms.CapitalizeInterest ? lines.Sum(x => x.CapitalizedInterest) : 0);
                if (plannedPrincipal != expectedPrincipal)
                    issues.Add(new ConsistencyIssue("PLAN_PRINCIPAL", loan.Id,
                        "El capital del plan no concilia con el desembolso y sus capitalizaciones."));
            }
        }
        foreach (var receipt in receipts)
        {
            var applied = allocations.Where(x => x.ReceiptId == receipt.Id).ToList();
            if (receipt.Received != receipt.Applied + receipt.Credit ||
                applied.Sum(x => x.Amount) != receipt.Applied)
                issues.Add(new ConsistencyIssue("RECEIPT_TOTAL", receipt.LoanId,
                    $"El recaudo {receipt.Id} no concilia con sus aplicaciones."));
            var paymentMovements = movements.Where(x => x.EventId == receipt.EventId &&
                x.LoanId == receipt.LoanId && x.Amount < 0).ToList();
            var expected = applied.GroupBy(x => (x.InstallmentId, x.Component))
                .ToDictionary(x => x.Key, x => x.Sum(y => y.Amount));
            var actual = paymentMovements.Where(x => x.InstallmentId.HasValue)
                .GroupBy(x => (InstallmentId: x.InstallmentId!.Value, x.Component))
                .ToDictionary(x => x.Key, x => -x.Sum(y => y.Amount));
            if (expected.Count != actual.Count || expected.Any(x =>
                !actual.TryGetValue(x.Key, out decimal value) || value != x.Value))
                issues.Add(new ConsistencyIssue("PAYMENT_LEDGER", receipt.LoanId,
                    $"El recaudo {receipt.Id} no concilia con sus movimientos."));
        }
        return new ConsistencyReport(DateTime.UtcNow, loans.Count, issues.Count, issues);
    }

    private static FinancialComponent Map(BalanceComponent component) => component switch
    {
        BalanceComponent.Principal => FinancialComponent.Principal,
        BalanceComponent.CurrentInterest => FinancialComponent.CurrentInterest,
        BalanceComponent.LateInterest => FinancialComponent.LateInterest,
        BalanceComponent.Insurance => FinancialComponent.Insurance,
        BalanceComponent.Charge => FinancialComponent.Charge,
        BalanceComponent.Credit => FinancialComponent.Credit,
        _ => throw new FinancialCalculationException("El libro tiene un componente desconocido.")
    };
}
