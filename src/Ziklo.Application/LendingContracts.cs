using Ziklo.Domain;
using Ziklo.FinancialEngine;

namespace Ziklo.Application;

public sealed record CreateClient(
    string IdentificationType, string IdentificationNumber, string Name,
    string? Address, string? Phone, string? WhatsApp, string? Email,
    string? City, DateOnly? BirthDate, string? EconomicActivity,
    decimal? MonthlyIncome, string? Employer);

public sealed record CreateProduct(string Code, string Name, string? Description);
public sealed record UpdateProduct(string Name, string? Description, bool IsActive);

public sealed record CreateClientReference(ClientReferenceKind Kind, string Name,
    string? Relationship, string? Phone, string? Email, string? Notes);

public sealed record CreateProductVersion(
    decimal MinimumAmount, decimal MaximumAmount,
    int MinimumInstallments, int MaximumInstallments,
    decimal Rate, RateKind RateKind, DayCountConvention DayCount,
    PaymentFrequency Frequency, AmortizationMethod Method,
    int CurrencyDecimals, bool AllowsCapitalization,
    decimal InsurancePerInstallment, decimal OtherChargePerInstallment,
    DateOnly ValidFrom, string CurrencyCode,
    decimal LateAnnualRate = 0, int LateGraceDays = 0,
    GraceMode LateGraceMode = GraceMode.Prospective,
    decimal? LateMaximumPerInstallment = null,
    IReadOnlyList<PaymentCategory>? PaymentPriority = null);

public sealed record CreateLoan(Guid ClientId, Guid ProductVersionId, string Number,
    DateOnly RequestDate, decimal Principal, int Installments, bool CapitalizeInterest = false);

public sealed record ApproveLoan(DateOnly ApprovalDate);
public sealed record RejectLoan(DateOnly DecisionDate, string Reason);
public sealed record DisburseLoan(DateOnly DisbursementDate, DateOnly FirstDueDate,
    string IdempotencyKey);

public sealed record ReceivePayment(DateOnly EffectiveDate, decimal Amount,
    string Method, string ExternalReference, string IdempotencyKey,
    IReadOnlyList<int>? InstallmentNumbers = null);

public sealed record PaymentReceiptView(Guid Id, Guid LoanId, DateOnly EffectiveDate,
    decimal Received, decimal Applied, decimal Credit, string CurrencyCode,
    string Method, string ExternalReference, string IdempotencyKey,
    IReadOnlyList<PaymentAllocationView> Allocations);

public sealed record PaymentAllocationView(Guid InstallmentId, BalanceComponent Component,
    decimal Amount, int Order);

public sealed record RunDailyClose(DateOnly BusinessDate);
public sealed record DailyCloseView(Guid Id, DateOnly BusinessDate, DateTime CompletedAtUtc,
    int LoansProcessed, int AccrualEvents, int LateEvents, int PromisesEvaluated);

public interface IDailyCloseService
{
    Task<DailyCloseView> RunAsync(Guid companyId, RunDailyClose command,
        Guid actorUserId, CancellationToken ct);
}

public sealed record PortfolioLoanView(Guid LoanId, Guid ClientId, string LoanNumber,
    string CurrencyCode, int DaysPastDue, string AgingBucket, decimal Principal,
    decimal CurrentInterest, decimal LateInterest, decimal Insurance,
    decimal Charges, decimal Credit, decimal Total);

public sealed record PortfolioView(DateOnly AsOf, int LoanCount, int DelinquentLoanCount,
    decimal Principal, decimal CurrentInterest, decimal LateInterest,
    decimal Insurance, decimal Charges, decimal Credit, decimal Total,
    IReadOnlyList<PortfolioLoanView> Loans);

public sealed record ClientPortfolioSummary(Guid ClientId, string Name,
    string IdentificationType, string IdentificationNumber, DateOnly AsOf,
    int ActiveLoanCount, int PaidOffLoanCount, int DelinquentLoanCount,
    decimal PrincipalOutstanding, decimal CurrentInterestOutstanding,
    decimal LateInterestOutstanding, decimal TotalOutstanding,
    int PaymentCount, decimal TotalReceived,
    IReadOnlyList<PortfolioLoanView> Loans);

public interface IPortfolioService
{
    Task<PortfolioView> GetAsync(Guid companyId, DateOnly asOf, CancellationToken ct);
    Task<ClientPortfolioSummary?> GetClientAsync(Guid companyId, Guid clientId,
        DateOnly asOf, CancellationToken ct);
}

public sealed record CreateCollectionActivity(DateOnly ContactDate,
    CollectionChannel Channel, string Result, string Notes,
    DateOnly? NextContactDate, DateOnly? PromiseDueDate,
    decimal? PromisedAmount);

public sealed record CollectionActivityView(Guid Id, Guid LoanId, Guid ClientId,
    DateOnly ContactDate, CollectionChannel Channel, string Result, string Notes,
    DateOnly? NextContactDate, Guid? PromiseId, DateOnly? PromiseDueDate,
    decimal? PromisedAmount, PromiseStatus? PromiseStatus);

public interface ICollectionService
{
    Task<CollectionActivityView> CreateAsync(Guid companyId, Guid loanId,
        CreateCollectionActivity command, Guid actorUserId, CancellationToken ct);
    Task<IReadOnlyList<CollectionActivityView>> ListAsync(Guid companyId, Guid loanId,
        CancellationToken ct);
}

public sealed record AgingSummary(string Bucket, int LoanCount, decimal Total);

public sealed record DashboardView(DateOnly AsOf, decimal CapitalDisbursed,
    decimal PortfolioTotal, decimal PrincipalOutstanding, decimal CurrentInterestOutstanding,
    decimal LateInterestOutstanding, decimal ReceiptsToday, decimal ReceiptsThisMonth,
    decimal CurrentInterestAccrued, decimal CurrentInterestCollected,
    decimal LateInterestAccrued, decimal LateInterestCollected,
    int ActiveLoanCount, int DelinquentLoanCount, int PaidOffLoanCount,
    IReadOnlyList<AgingSummary> Aging);

public interface IDashboardService
{
    Task<DashboardView> GetAsync(Guid companyId, DateOnly asOf, CancellationToken ct);
}

public sealed record SetPermission(bool Allowed);
public sealed record PermissionView(InitialRole Role, BusinessPermission Permission,
    bool Allowed, bool IsOverride);

public interface IPermissionService
{
    Task<IReadOnlyList<PermissionView>> ListAsync(Guid companyId, CancellationToken ct);
    Task<PermissionView> SetAsync(Guid companyId, InitialRole role,
        BusinessPermission permission, bool allowed, Guid actorUserId,
        string ipAddress, CancellationToken ct);
}

public sealed record ConsistencyIssue(string Code, Guid? LoanId, string Detail);
public sealed record ConsistencyReport(DateTime CheckedAtUtc, int LoanCount,
    int IssueCount, IReadOnlyList<ConsistencyIssue> Issues);

public interface IConsistencyService
{
    Task<ConsistencyReport> CheckAsync(Guid companyId, CancellationToken ct);
}

public sealed record StatementMovement(long Sequence, DateOnly EffectiveDate,
    LoanEventType EventType, BalanceComponent Component, decimal Amount,
    Guid? InstallmentId);

public sealed record StatementReceipt(Guid Id, DateOnly EffectiveDate, decimal Received,
    string Method, string ExternalReference);

public sealed record LoanStatement(Guid LoanId, Guid ClientId, string LoanNumber,
    DateOnly AsOf, decimal OriginalPrincipal, decimal PrincipalOutstanding,
    decimal CurrentInterestOutstanding, decimal LateInterestOutstanding,
    decimal InsuranceOutstanding, decimal ChargesOutstanding, decimal Credit,
    decimal TotalOutstanding, decimal PrincipalPaid, decimal CurrentInterestPaid,
    decimal LateInterestPaid, DateOnly? NextDueDate, int PastDueInstallments,
    int DaysPastDue, IReadOnlyList<StatementReceipt> Receipts,
    IReadOnlyList<StatementMovement> Movements);

public interface IStatementService
{
    Task<LoanStatement?> GetAsync(Guid companyId, Guid loanId,
        DateOnly asOf, CancellationToken ct);
}

public interface IPaymentService
{
    Task<PaymentReceiptView> ReceiveAsync(Guid companyId, Guid loanId,
        ReceivePayment command, Guid actorUserId, CancellationToken ct);
    Task<PaymentReceiptView?> GetAsync(Guid companyId, Guid loanId,
        Guid receiptId, CancellationToken ct);
}

public interface ILendingService
{
    Task<Client> CreateClientAsync(Guid companyId, CreateClient command, CancellationToken ct);
    Task<Client?> GetClientAsync(Guid companyId, Guid clientId, CancellationToken ct);
    Task<ClientReference> CreateClientReferenceAsync(Guid companyId, Guid clientId,
        CreateClientReference command, CancellationToken ct);
    Task<CreditProduct> CreateProductAsync(Guid companyId, CreateProduct command, CancellationToken ct);
    Task<ProductVersion> CreateProductVersionAsync(Guid companyId, Guid productId,
        CreateProductVersion command, CancellationToken ct);
    Task<Loan> CreateLoanAsync(Guid companyId, CreateLoan command, CancellationToken ct);
    Task<Loan> RequestLoanAsync(Guid companyId, Guid loanId, CancellationToken ct);
    Task<Loan> ApproveLoanAsync(Guid companyId, Guid loanId, ApproveLoan command, CancellationToken ct);
    Task<Loan> RejectLoanAsync(Guid companyId, Guid loanId, RejectLoan command,
        Guid actorUserId, CancellationToken ct);
    Task<Loan> DisburseLoanAsync(Guid companyId, Guid loanId, DisburseLoan command,
        Guid actorUserId, CancellationToken ct);
}

public sealed class LendingRuleException(string message) : Exception(message);
