using Ziklo.FinancialEngine;

namespace Ziklo.Domain;

public abstract class CompanyRecord
{
    public Guid CompanyId { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class Company
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string TaxIdentifier { get; set; } = string.Empty;
    public string TimeZoneId { get; set; } = "America/Bogota";
    public string CurrencyCode { get; set; } = "COP";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class UserAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public enum InitialRole { Administrator, Manager, CreditAnalyst, Cashier, Collector, Auditor, ReadOnly, Originator, CreditApprover }

public sealed class UserCompanyMembership
{
    public Guid CompanyId { get; set; }
    public Guid UserId { get; set; }
    public InitialRole Role { get; set; }
    public Guid? ProfileId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime GrantedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class AccessProfile : CompanyRecord
{
    public string Name { get; set; } = string.Empty;
    public long PermissionMask { get; set; }
    public bool IsActive { get; set; } = true;
}

public enum BusinessPermission
{
    ViewClients, ManageClients, ViewLoans, CreateLoan, ApproveLoan,
    DisburseLoan, RegisterPayment, RunDailyClose, RecordCollection,
    ViewReports, ViewAudit, ManageProducts, ManagePermissions,
    StudyApplications
}

public sealed class CompanyRolePermission
{
    public Guid CompanyId { get; set; }
    public InitialRole Role { get; set; }
    public BusinessPermission Permission { get; set; }
    public bool Allowed { get; set; }
    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid ChangedByUserId { get; set; }
    public byte[] Version { get; set; } = [];
}

public sealed class PermissionChange
{
    public Guid CompanyId { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public InitialRole Role { get; set; }
    public BusinessPermission Permission { get; set; }
    public bool PreviousAllowed { get; set; }
    public bool NewAllowed { get; set; }
    public Guid ActorUserId { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class AuditEntry
{
    public Guid CompanyId { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EntityName { get; set; } = string.Empty;
    public string RecordKey { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string BeforeJson { get; set; } = string.Empty;
    public string AfterJson { get; set; } = string.Empty;
    public Guid? ActorUserId { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class OutboxMessage
{
    public Guid CompanyId { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EventType { get; set; } = string.Empty;
    public Guid AggregateId { get; set; }
    public string PayloadJson { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAtUtc { get; set; }
    public int DeliveryAttempts { get; set; }
}

public enum ClientStatus { Active, Inactive }

public sealed class Client : CompanyRecord
{
    public string IdentificationType { get; set; } = string.Empty;
    public string IdentificationNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? WhatsApp { get; set; }
    public string? Email { get; set; }
    public string? City { get; set; }
    public DateOnly? BirthDate { get; set; }
    public string? EconomicActivity { get; set; }
    public decimal? MonthlyIncome { get; set; }
    public string? Employer { get; set; }
    public ClientStatus Status { get; set; } = ClientStatus.Active;
    public byte[] Version { get; set; } = [];
}

public enum ClientReferenceKind { Personal, Commercial }

public sealed class ClientReference : CompanyRecord
{
    public Guid ClientId { get; set; }
    public ClientReferenceKind Kind { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Relationship { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Notes { get; set; }
}

public sealed class ClientDocument : CompanyRecord
{
    public Guid ClientId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Length { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public byte[] Content { get; set; } = [];
    public Guid UploadedByUserId { get; set; }
}

public sealed class CreditProduct : CompanyRecord
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public byte[] Version { get; set; } = [];
}

public sealed class ProductVersion : CompanyRecord
{
    public Guid ProductId { get; set; }
    public int Number { get; set; }
    public bool IsPublished { get; set; }
    public DateOnly? ValidFrom { get; set; }
    public string CurrencyCode { get; set; } = "COP";
    public decimal MinimumAmount { get; set; }
    public decimal MaximumAmount { get; set; }
    public int MinimumInstallments { get; set; }
    public int MaximumInstallments { get; set; }
    public decimal Rate { get; set; }
    public RateKind RateKind { get; set; }
    public DayCountConvention DayCount { get; set; }
    public PaymentFrequency Frequency { get; set; }
    public AmortizationMethod Method { get; set; }
    public int CurrencyDecimals { get; set; } = 2;
    public bool AllowsCapitalization { get; set; }
    public decimal InsurancePerInstallment { get; set; }
    public decimal OtherChargePerInstallment { get; set; }
    public decimal LateAnnualRate { get; set; }
    public int LateGraceDays { get; set; }
    public GraceMode LateGraceMode { get; set; }
    public decimal? LateMaximumPerInstallment { get; set; }
    public string PaymentPriority { get; set; } = "Charges,LateInterest,OverdueCurrentInterest,OverduePrincipal,CurrentInterest,Principal";
}

public enum LoanStatus
{
    Draft, Requested, Approved, Rejected, Disbursed, Active, InArrears,
    Refinanced, PaidOff, ChargedOff
}

public enum CreditApplicationStatus
{
    Submitted, InReview, NeedsInformation, Approved, Rejected, Withdrawn, Disbursed
}

public sealed class CreditApplication : CompanyRecord
{
    public string Reference { get; set; } = string.Empty;
    public Guid? SubmittedByUserId { get; set; }
    public Guid? LoanId { get; set; }
    public Guid ProductVersionId { get; set; }
    public string IdentificationType { get; set; } = string.Empty;
    public string IdentificationNumber { get; set; } = string.Empty;
    public string ApplicantName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string EconomicActivity { get; set; } = string.Empty;
    public decimal MonthlyIncome { get; set; }
    public decimal MonthlyObligations { get; set; }
    public decimal RequestedAmount { get; set; }
    public int RequestedInstallments { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public string FormDataJson { get; set; } = "{}";
    public CreditApplicationStatus Status { get; set; } = CreditApplicationStatus.Submitted;
    public string AcknowledgementVersion { get; set; } = string.Empty;
    public DateTime AcknowledgedAtUtc { get; set; }
}

public sealed class CreditApplicationReview : CompanyRecord
{
    public Guid ApplicationId { get; set; }
    public Guid ActorUserId { get; set; }
    public CreditApplicationStatus PreviousStatus { get; set; }
    public CreditApplicationStatus NewStatus { get; set; }
    public string Notes { get; set; } = string.Empty;
    public decimal? VerifiedMonthlyIncome { get; set; }
    public decimal? VerifiedMonthlyObligations { get; set; }
    public DateTime RecordedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class CreditApplicationDocument : CompanyRecord
{
    public Guid ApplicationId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Length { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public byte[] Content { get; set; } = [];
}

public sealed class Loan : CompanyRecord
{
    public Guid ClientId { get; set; }
    public Guid ProductId { get; set; }
    public Guid ProductVersionId { get; set; }
    public string Number { get; set; } = string.Empty;
    public DateOnly RequestDate { get; set; }
    public DateOnly? ApprovalDate { get; set; }
    public DateOnly? DisbursementDate { get; set; }
    public DateOnly? FirstDueDate { get; set; }
    public decimal OriginalPrincipal { get; set; }
    public int RequestedInstallments { get; set; }
    public bool RequestedCapitalization { get; set; }
    public string CurrencyCode { get; set; } = "COP";
    public LoanStatus Status { get; set; } = LoanStatus.Draft;
    public LoanTerms? Terms { get; set; }
    public byte[] Version { get; set; } = [];
}

public sealed class LoanRejection : CompanyRecord
{
    public Guid LoanId { get; set; }
    public DateOnly DecisionDate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid ActorUserId { get; set; }
}

public sealed class LoanTerms
{
    public Guid CompanyId { get; set; }
    public Guid LoanId { get; set; }
    public Guid SourceProductVersionId { get; set; }
    public decimal Rate { get; set; }
    public RateKind RateKind { get; set; }
    public DayCountConvention DayCount { get; set; }
    public PaymentFrequency Frequency { get; set; }
    public AmortizationMethod Method { get; set; }
    public int Installments { get; set; }
    public int CurrencyDecimals { get; set; }
    public bool CapitalizeInterest { get; set; }
    public decimal InsurancePerInstallment { get; set; }
    public decimal OtherChargePerInstallment { get; set; }
    public decimal LateAnnualRate { get; set; }
    public int LateGraceDays { get; set; }
    public GraceMode LateGraceMode { get; set; }
    public decimal? LateMaximumPerInstallment { get; set; }
    public string PaymentPriority { get; set; } = "Charges,LateInterest,OverdueCurrentInterest,OverduePrincipal,CurrentInterest,Principal";
    public DateTime CapturedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class LoanPlanVersion : CompanyRecord
{
    public Guid LoanId { get; set; }
    public int Number { get; set; }
    public bool IsCurrent { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid? PreviousPlanId { get; set; }
}

public sealed class LoanInstallment : CompanyRecord
{
    public Guid LoanId { get; set; }
    public Guid PlanId { get; set; }
    public int Number { get; set; }
    public DateOnly DueDate { get; set; }
    public decimal OpeningPrincipal { get; set; }
    public decimal Principal { get; set; }
    public decimal Interest { get; set; }
    public decimal CapitalizedInterest { get; set; }
    public decimal Insurance { get; set; }
    public decimal OtherCharges { get; set; }
    public decimal Payment { get; set; }
    public decimal ClosingPrincipal { get; set; }
}

public enum LoanEventType { Disbursement, CurrentInterestAccrual, LateInterestAccrual, Payment,
    PrincipalPrepayment, Capitalization, Waiver, Adjustment, Refinancing, ChargeOff, Reversal }
public enum BalanceComponent { Principal, CurrentInterest, LateInterest, Insurance, Charge, Credit }

public sealed class LoanEvent : CompanyRecord
{
    public Guid LoanId { get; set; }
    public LoanEventType Type { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public string OriginReference { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public Guid? ReversesEventId { get; set; }
    public string? Reason { get; set; }
    public Guid? ActorUserId { get; set; }
}

public sealed class LoanMovement : CompanyRecord
{
    public Guid LoanId { get; set; }
    public Guid EventId { get; set; }
    public Guid? InstallmentId { get; set; }
    public long Sequence { get; set; }
    public BalanceComponent Component { get; set; }
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = "COP";
}

public enum ReceiptStatus { Applied, Reversed }

public sealed class PaymentReceipt : CompanyRecord
{
    public Guid LoanId { get; set; }
    public Guid ClientId { get; set; }
    public Guid EventId { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public decimal Received { get; set; }
    public decimal Applied { get; set; }
    public decimal Credit { get; set; }
    public string CurrencyCode { get; set; } = "COP";
    public string Method { get; set; } = string.Empty;
    public string ExternalReference { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public Guid ActorUserId { get; set; }
    public ReceiptStatus Status { get; set; } = ReceiptStatus.Applied;
}

public sealed class PaymentAllocationRecord : CompanyRecord
{
    public Guid LoanId { get; set; }
    public Guid ReceiptId { get; set; }
    public Guid InstallmentId { get; set; }
    public BalanceComponent Component { get; set; }
    public decimal Amount { get; set; }
    public int Order { get; set; }
}

public sealed class DailyCloseRun
{
    public Guid CompanyId { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateOnly BusinessDate { get; set; }
    public DateTime CompletedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid ActorUserId { get; set; }
    public int LoansProcessed { get; set; }
    public int AccrualEvents { get; set; }
    public int LateEvents { get; set; }
    public int PromisesEvaluated { get; set; }
}

public sealed class LateInterestAssessment : CompanyRecord
{
    public Guid LoanId { get; set; }
    public Guid InstallmentId { get; set; }
    public Guid EventId { get; set; }
    public DateOnly DueDate { get; set; }
    public DateOnly CutoffDate { get; set; }
    public int EligibleDays { get; set; }
    public decimal BaseAtCutoff { get; set; }
    public decimal AnnualRate { get; set; }
    public decimal AlreadyPosted { get; set; }
    public decimal CumulativeInterest { get; set; }
    public decimal AmountPosted { get; set; }
    public int GraceDays { get; set; }
    public GraceMode GraceMode { get; set; }
    public DayCountConvention DayCount { get; set; }
}

public enum CollectionChannel { Call, WhatsApp, Email, Sms, Visit, Other }

public sealed class CollectionActivity : CompanyRecord
{
    public Guid LoanId { get; set; }
    public Guid ClientId { get; set; }
    public Guid ActorUserId { get; set; }
    public DateOnly ContactDate { get; set; }
    public CollectionChannel Channel { get; set; }
    public string Result { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public DateOnly? NextContactDate { get; set; }
}

public enum PromiseStatus { Pending, Fulfilled, Partial, Broken }

public sealed class PaymentPromise : CompanyRecord
{
    public Guid LoanId { get; set; }
    public Guid CollectionActivityId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly DueDate { get; set; }
    public decimal PromisedAmount { get; set; }
    public PromiseStatus Status { get; set; } = PromiseStatus.Pending;
    public DateOnly? EvaluatedOn { get; set; }
    public decimal? ReceivedAmount { get; set; }
    public byte[] Version { get; set; } = [];
}

public sealed class PromiseEvaluation : CompanyRecord
{
    public Guid LoanId { get; set; }
    public Guid PromiseId { get; set; }
    public Guid DailyCloseRunId { get; set; }
    public PromiseStatus Status { get; set; }
    public decimal ReceivedAmount { get; set; }
    public Guid ActorUserId { get; set; }
}
