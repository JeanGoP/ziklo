using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Ziklo.Application;
using Ziklo.Domain;

namespace Ziklo.Infrastructure;

public sealed class ReportCatalogService(ZikloDbContext db,
    IPortfolioService portfolioService, IPaymentReportService paymentReportService)
    : IReportCatalogService
{
    private static readonly string[] Buckets = ["Por vencer", "1-30", "31-60",
        "61-90", "91-120", "121-150", "151-180", "Más de 180"];

    public async Task<ReportTable> GetAsync(Guid companyId, string kind,
        DateOnly from, DateOnly to, CancellationToken ct)
    {
        var company = await db.Companies.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == companyId && x.IsActive, ct)
            ?? throw new LendingRuleException("La empresa no existe o está inactiva.");
        if (from == default || to < from || to > DailyCloseService.BusinessDate(company) ||
            to.DayNumber - from.DayNumber > 366)
            throw new LendingRuleException("El informe requiere fechas válidas de máximo 367 días.");
        return kind.ToLowerInvariant() switch
        {
            "portfolio" => await Portfolio(companyId, from, to, false, ct),
            "overdue" => await Portfolio(companyId, from, to, true, ct),
            "aging" => await Aging(companyId, from, to, ct),
            "delinquent-clients" => await DelinquentClients(companyId, from, to, ct),
            "payments" => await Payments(companyId, from, to, ct),
            "interest" => await Interest(companyId, from, to, ct),
            "disbursements" => await Disbursements(companyId, from, to, ct),
            "paid-off" => await PaidOff(companyId, from, to, ct),
            "promises" => await Promises(companyId, from, to, ct),
            _ => throw new LendingRuleException("El tipo de informe no existe.")
        };
    }

    private async Task<ReportTable> Portfolio(Guid companyId, DateOnly from,
        DateOnly to, bool overdue, CancellationToken ct)
    {
        var portfolio = await portfolioService.GetAsync(companyId, to, ct);
        var loans = portfolio.Loans.Where(x => !overdue || x.DaysPastDue > 0).ToList();
        var clientIds = loans.Select(x => x.ClientId).Distinct().ToList();
        var clients = await db.Clients.AsNoTracking().Where(x =>
            x.CompanyId == companyId && clientIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var rows = loans.Select(x => new[] { x.LoanNumber,
            clients.GetValueOrDefault(x.ClientId, ""), x.DaysPastDue.ToString(),
            x.AgingBucket, Amount(x.Principal), Amount(x.CurrentInterest),
            Amount(x.LateInterest), Amount(x.Insurance + x.Charges),
            Amount(x.Total) }).ToList();
        return new ReportTable(overdue ? "overdue" : "portfolio",
            overdue ? "Cartera vencida" : "Cartera general", from, to,
            ["Préstamo", "Cliente", "Días de atraso", "Edad", "Capital",
             "Interés", "Mora", "Seguros y cargos", "Total"],
            [2, 4, 5, 6, 7, 8], rows,
            $"Saldos publicados al {to:yyyy-MM-dd}. {loans.Count} préstamos; total {Money(loans.Sum(x => x.Total))}.");
    }

    private async Task<ReportTable> Aging(Guid companyId, DateOnly from,
        DateOnly to, CancellationToken ct)
    {
        var portfolio = await portfolioService.GetAsync(companyId, to, ct);
        var rows = Buckets.Select(bucket =>
        {
            var loans = portfolio.Loans.Where(x => x.AgingBucket == bucket).ToList();
            return new[] { bucket, loans.Count.ToString(), Amount(loans.Sum(x => x.Principal)),
                Amount(loans.Sum(x => x.CurrentInterest)),
                Amount(loans.Sum(x => x.LateInterest)), Amount(loans.Sum(x => x.Total)) };
        }).ToList();
        return new ReportTable("aging", "Cartera por edades", from, to,
            ["Edad", "Préstamos", "Capital", "Interés", "Mora", "Total"],
            [1, 2, 3, 4, 5], rows, $"Distribución al {to:yyyy-MM-dd}.");
    }

    private async Task<ReportTable> DelinquentClients(Guid companyId, DateOnly from,
        DateOnly to, CancellationToken ct)
    {
        var portfolio = await portfolioService.GetAsync(companyId, to, ct);
        var overdue = portfolio.Loans.Where(x => x.DaysPastDue > 0).ToList();
        var clientIds = overdue.Select(x => x.ClientId).Distinct().ToList();
        var clients = await db.Clients.AsNoTracking().Where(x =>
            x.CompanyId == companyId && clientIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => new { x.Name, x.IdentificationNumber }, ct);
        var rows = overdue.GroupBy(x => x.ClientId).Select(group => new[]
        {
            clients[group.Key].Name, clients[group.Key].IdentificationNumber,
            group.Count().ToString(), group.Max(x => x.DaysPastDue).ToString(),
            Amount(group.Sum(x => x.Principal)), Amount(group.Sum(x => x.Total))
        }).OrderByDescending(x => decimal.Parse(x[5], CultureInfo.InvariantCulture)).ToList();
        return new ReportTable("delinquent-clients", "Clientes morosos", from, to,
            ["Cliente", "Identificación", "Préstamos", "Máximo atraso",
             "Capital", "Total"], [2, 3, 4, 5], rows,
            $"Clientes con obligaciones vencidas al {to:yyyy-MM-dd}.");
    }

    private async Task<ReportTable> Payments(Guid companyId, DateOnly from,
        DateOnly to, CancellationToken ct)
    {
        var report = await paymentReportService.GetAsync(companyId, from, to, ct);
        var rows = report.Rows.Select(x => new[] { x.EffectiveDate.ToString("yyyy-MM-dd"),
            x.LoanNumber, x.ClientName, x.Method, x.ExternalReference,
            x.ReceivedBy, Amount(x.Received), Amount(x.Applied) }).ToList();
        return new ReportTable("payments", "Recaudos", from, to,
            ["Fecha", "Préstamo", "Cliente", "Medio", "Referencia",
             "Usuario", "Recibido", "Aplicado"], [6, 7], rows,
            $"{report.Count} recaudos; recibido {Money(report.TotalReceived)}; aplicado {Money(report.TotalApplied)}.");
    }

    private async Task<ReportTable> Interest(Guid companyId, DateOnly from,
        DateOnly to, CancellationToken ct)
    {
        var events = await db.LoanEvents.AsNoTracking().Where(x =>
            x.CompanyId == companyId && x.EffectiveDate >= from && x.EffectiveDate <= to)
            .Select(x => new { x.Id, x.Type }).ToListAsync(ct);
        var eventTypes = events.ToDictionary(x => x.Id, x => x.Type);
        var eventIds = events.Select(x => x.Id).ToList();
        var movements = await db.LoanMovements.AsNoTracking().Where(x =>
            x.CompanyId == companyId && eventIds.Contains(x.EventId) &&
            (x.Component == BalanceComponent.CurrentInterest ||
             x.Component == BalanceComponent.LateInterest))
            .ToListAsync(ct);
        var loanIds = movements.Select(x => x.LoanId).Distinct().ToList();
        var loans = await db.Loans.AsNoTracking().Where(x =>
            x.CompanyId == companyId && loanIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Number, ct);
        decimal Flow(IEnumerable<LoanMovement> items, BalanceComponent component,
            bool collected) => items.Where(x => x.Component == component &&
                eventTypes.TryGetValue(x.EventId, out var type) &&
                (collected ? type == LoanEventType.Payment && x.Amount < 0 : x.Amount > 0))
                .Sum(x => collected ? -x.Amount : x.Amount);
        var rows = movements.GroupBy(x => x.LoanId)
            .OrderBy(group => loans.GetValueOrDefault(group.Key))
            .Select(group => new[] { loans.GetValueOrDefault(group.Key, ""),
                Amount(Flow(group, BalanceComponent.CurrentInterest, false)),
                Amount(Flow(group, BalanceComponent.CurrentInterest, true)),
                Amount(Flow(group, BalanceComponent.LateInterest, false)),
                Amount(Flow(group, BalanceComponent.LateInterest, true)) }).ToList();
        return new ReportTable("interest", "Intereses y mora", from, to,
            ["Préstamo", "Interés causado", "Interés cobrado",
             "Mora causada", "Mora cobrada"], [1, 2, 3, 4], rows,
            "Movimientos confirmados en el libro durante el periodo. No incluye intereses futuros.");
    }

    private async Task<ReportTable> Disbursements(Guid companyId, DateOnly from,
        DateOnly to, CancellationToken ct)
    {
        var loans = await db.Loans.AsNoTracking().Where(x =>
            x.CompanyId == companyId && x.DisbursementDate >= from &&
            x.DisbursementDate <= to).OrderBy(x => x.DisbursementDate)
            .ThenBy(x => x.Number).ToListAsync(ct);
        var ids = loans.Select(x => x.ClientId).Distinct().ToList();
        var clients = await db.Clients.AsNoTracking().Where(x =>
            x.CompanyId == companyId && ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var rows = loans.Select(x => new[] { x.DisbursementDate!.Value.ToString("yyyy-MM-dd"),
            x.Number, clients.GetValueOrDefault(x.ClientId, ""),
            Amount(x.OriginalPrincipal), x.RequestedInstallments.ToString(),
            LoanStatusLabel(x.Status) }).ToList();
        return new ReportTable("disbursements", "Préstamos desembolsados", from, to,
            ["Fecha", "Préstamo", "Cliente", "Capital", "Cuotas", "Estado"],
            [3, 4], rows, $"{loans.Count} desembolsos; capital {Money(loans.Sum(x => x.OriginalPrincipal))}.");
    }

    private async Task<ReportTable> PaidOff(Guid companyId, DateOnly from,
        DateOnly to, CancellationToken ct)
    {
        var paidLoans = await db.Loans.AsNoTracking().Where(x =>
            x.CompanyId == companyId && x.Status == LoanStatus.PaidOff).ToListAsync(ct);
        var ids = paidLoans.Select(x => x.Id).ToList();
        var receipts = await db.PaymentReceipts.AsNoTracking().Where(x =>
            x.CompanyId == companyId && ids.Contains(x.LoanId))
            .Select(x => new { x.LoanId, x.EffectiveDate }).ToListAsync(ct);
        var dates = receipts.GroupBy(x => x.LoanId)
            .ToDictionary(x => x.Key, x => x.Max(y => y.EffectiveDate));
        var selected = paidLoans.Where(x => dates.TryGetValue(x.Id, out var date) &&
            date >= from && date <= to).ToList();
        var clientIds = selected.Select(x => x.ClientId).Distinct().ToList();
        var clients = await db.Clients.AsNoTracking().Where(x =>
            x.CompanyId == companyId && clientIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var rows = selected.OrderBy(x => dates[x.Id]).Select(x => new[]
        {
            dates[x.Id].ToString("yyyy-MM-dd"), x.Number,
            clients.GetValueOrDefault(x.ClientId, ""), Amount(x.OriginalPrincipal)
        }).ToList();
        return new ReportTable("paid-off", "Préstamos cancelados", from, to,
            ["Último pago", "Préstamo", "Cliente", "Capital original"], [3], rows,
            "La fecha de cancelación se infiere del último recaudo del préstamo cancelado.");
    }

    private async Task<ReportTable> Promises(Guid companyId, DateOnly from,
        DateOnly to, CancellationToken ct)
    {
        var promises = await db.PaymentPromises.AsNoTracking().Where(x =>
            x.CompanyId == companyId && x.DueDate >= from && x.DueDate <= to)
            .OrderBy(x => x.DueDate).ToListAsync(ct);
        var ids = promises.Select(x => x.LoanId).Distinct().ToList();
        var loans = await db.Loans.AsNoTracking().Where(x =>
            x.CompanyId == companyId && ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Number, ct);
        var rows = promises.Select(x => new[] { x.DueDate.ToString("yyyy-MM-dd"),
            loans.GetValueOrDefault(x.LoanId, ""), Amount(x.PromisedAmount),
            Amount(x.ReceivedAmount ?? 0), x.Status.ToString() }).ToList();
        return new ReportTable("promises", "Promesas de pago", from, to,
            ["Vencimiento", "Préstamo", "Prometido", "Recibido", "Estado"],
            [2, 3], rows, "Estado actual de las promesas con vencimiento en el periodo.");
    }

    private static string Amount(decimal value) =>
        value.ToString("0.00##", CultureInfo.InvariantCulture);

    private static string Money(decimal value) =>
        value.ToString("C2", CultureInfo.GetCultureInfo("es-CO"));

    private static string LoanStatusLabel(LoanStatus status) => status switch
    {
        LoanStatus.Active => "Activo",
        LoanStatus.InArrears => "En mora",
        LoanStatus.PaidOff => "Cancelado",
        LoanStatus.Refinanced => "Refinanciado",
        LoanStatus.ChargedOff => "Castigado",
        _ => status.ToString()
    };
}
