namespace Ziklo.FinancialEngine;

public enum RateKind { AnnualSimple, PeriodicSimple, EffectiveAnnual, PeriodicEffective, NominalAnnual }
public enum DayCountConvention { Actual365Fixed, Actual360, ActualActual }
public enum PaymentFrequency { Daily, Weekly, Every15Days, SemiMonthly, Monthly, Quarterly, SemiAnnual, Annual }
public enum AmortizationMethod { French, FixedPrincipal, SimplePeriodic, Bullet, Custom }

public sealed record SimulationRequest(
    decimal Principal,
    decimal Rate,
    RateKind RateKind,
    DayCountConvention DayCount,
    PaymentFrequency Frequency,
    int Installments,
    DateOnly DisbursementDate,
    DateOnly FirstDueDate,
    AmortizationMethod Method,
    int CurrencyDecimals = 2,
    bool CapitalizeInterest = false,
    IReadOnlyList<decimal>? CustomPrincipal = null,
    decimal InsurancePerInstallment = 0,
    decimal OtherChargePerInstallment = 0);

public sealed record ScheduleLine(
    int Number,
    DateOnly DueDate,
    decimal OpeningPrincipal,
    decimal Principal,
    decimal Interest,
    decimal CapitalizedInterest,
    decimal Insurance,
    decimal OtherCharges,
    decimal Payment,
    decimal ClosingPrincipal);

public sealed record SimulationResult(
    decimal OriginalPrincipal,
    decimal TotalPrincipalRepaid,
    decimal TotalInterest,
    decimal TotalInsurance,
    decimal TotalOtherCharges,
    decimal TotalPayable,
    decimal EstimatedPayment,
    DateOnly FinalDueDate,
    IReadOnlyList<ScheduleLine> Schedule);

public sealed class FinancialCalculationException(string message) : Exception(message);
