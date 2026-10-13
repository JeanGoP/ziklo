using Microsoft.EntityFrameworkCore;
using Ziklo.Application;

namespace Ziklo.Infrastructure;

public sealed class PaymentReportService(ZikloDbContext db) : IPaymentReportService
{
    public async Task<PaymentReport> GetAsync(Guid companyId, DateOnly from,
        DateOnly to, CancellationToken ct)
    {
        if (from == default || to < from || to.DayNumber - from.DayNumber > 366)
            throw new LendingRuleException("El reporte requiere un rango válido de máximo 367 días.");
        var query = db.PaymentReceipts.AsNoTracking().Where(x =>
            x.CompanyId == companyId && x.EffectiveDate >= from && x.EffectiveDate <= to);
        if (await query.CountAsync(ct) > 50000)
            throw new LendingRuleException("El reporte supera 50.000 recaudos. Reduce el rango de fechas.");
        var receipts = await query.OrderBy(x => x.EffectiveDate).ThenBy(x => x.CreatedAtUtc)
            .ThenBy(x => x.Id).ToListAsync(ct);
        var loanIds = receipts.Select(x => x.LoanId).Distinct().ToList();
        var clientIds = receipts.Select(x => x.ClientId).Distinct().ToList();
        var userIds = receipts.Select(x => x.ActorUserId).Distinct().ToList();
        var loans = await db.Loans.AsNoTracking().Where(x =>
            x.CompanyId == companyId && loanIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Number, ct);
        var clients = await db.Clients.AsNoTracking().Where(x =>
            x.CompanyId == companyId && clientIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var users = await db.Users.AsNoTracking().Where(x => userIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Email, ct);
        var rows = receipts.Select(x => new PaymentReportRow(x.Id, x.EffectiveDate,
            loans.GetValueOrDefault(x.LoanId, "[préstamo no disponible]"),
            clients.GetValueOrDefault(x.ClientId, "[cliente no disponible]"),
            x.Method, x.ExternalReference,
            users.GetValueOrDefault(x.ActorUserId, "[usuario no disponible]"),
            x.Received, x.Applied, x.CurrencyCode)).ToList();
        var methods = rows.GroupBy(x => x.Method)
            .OrderBy(x => x.Key)
            .Select(group => new PaymentMethodTotal(group.Key, group.Count(),
                group.Sum(x => x.Received), group.Sum(x => x.Applied))).ToList();
        return new PaymentReport(from, to, rows.Count, rows.Sum(x => x.Received),
            rows.Sum(x => x.Applied), methods, rows);
    }
}
