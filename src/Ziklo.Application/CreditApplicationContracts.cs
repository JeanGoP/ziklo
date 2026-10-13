using Ziklo.Domain;

namespace Ziklo.Application;

public sealed record CreditApplicationUpload(string Kind, string FileName,
    string ContentType, byte[] Content);

public sealed record SubmitCreditApplication(Guid CompanyId, Guid ProductVersionId,
    string IdentificationType, string IdentificationNumber, string ApplicantName,
    string Email, string Phone, string City, string EconomicActivity,
    decimal MonthlyIncome, decimal MonthlyObligations, decimal RequestedAmount,
    int RequestedInstallments, string Purpose, string FormDataJson, bool DemoAcknowledged,
    IReadOnlyList<CreditApplicationUpload> Documents, Guid? SubmittedByUserId = null);

public sealed record CreditApplicationReceipt(string Reference, DateTime SubmittedAtUtc,
    CreditApplicationStatus Status, string ProductName, decimal RequestedAmount,
    int RequestedInstallments, int DocumentCount);

public sealed record RecordCreditApplicationReview(CreditApplicationStatus Status,
    string Notes, decimal? VerifiedMonthlyIncome,
    decimal? VerifiedMonthlyObligations);

public sealed record RespondToInformation(string Notes);

public interface ICreditApplicationService
{
    Task<CreditApplicationReceipt> SubmitAsync(SubmitCreditApplication request,
        CancellationToken ct);
    Task<CreditApplicationReview> ReviewAsync(Guid companyId, Guid applicationId,
        Guid actorUserId, RecordCreditApplicationReview request, CancellationToken ct);
    Task<Loan> ConvertApprovedAsync(Guid companyId, Guid applicationId,
        DateOnly approvalDate, CancellationToken ct);
    Task<CreditApplicationDocument> AttachSignedContractAsync(Guid companyId,
        Guid applicationId, string fileName, byte[] content, CancellationToken ct);
    Task<CreditApplicationReview> RespondToInformationAsync(Guid companyId,
        Guid applicationId, Guid actorUserId, string notes, CancellationToken ct);
    Task<CreditApplicationDocument> AttachSupplementAsync(Guid companyId,
        Guid applicationId, Guid actorUserId, string fileName, byte[] content,
        CancellationToken ct);
}
