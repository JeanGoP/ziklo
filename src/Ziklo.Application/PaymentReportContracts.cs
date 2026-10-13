namespace Ziklo.Application;

public sealed record PaymentReportRow(Guid ReceiptId, DateOnly EffectiveDate,
    string LoanNumber, string ClientName, string Method, string ExternalReference,
    string ReceivedBy, decimal Received, decimal Applied, string CurrencyCode);

public sealed record PaymentMethodTotal(string Method, int Count, decimal Received,
    decimal Applied);

public sealed record PaymentReport(DateOnly From, DateOnly To, int Count,
    decimal TotalReceived, decimal TotalApplied,
    IReadOnlyList<PaymentMethodTotal> ByMethod,
    IReadOnlyList<PaymentReportRow> Rows);

public interface IPaymentReportService
{
    Task<PaymentReport> GetAsync(Guid companyId, DateOnly from, DateOnly to,
        CancellationToken ct);
}
