namespace Ziklo.FinancialEngine;

public sealed class InterestEngine
{
    public decimal CalculateInterest(decimal principal, decimal rate, RateKind rateKind,
        DayCountConvention dayCount, PaymentFrequency frequency, DateOnly from, DateOnly to)
    {
        if (principal < 0 || rate < 0)
            throw new FinancialCalculationException("El capital y la tasa deben ser no negativos.");
        try { return principal * CalculateIntervalRate(rate, rateKind, dayCount, frequency, from, to); }
        catch (OverflowException)
        {
            throw new FinancialCalculationException("El interés excede la precisión decimal admitida.");
        }
    }

    public decimal CalculateIntervalRate(decimal rate, RateKind rateKind,
        DayCountConvention dayCount, PaymentFrequency frequency, DateOnly from, DateOnly to)
    {
        if (rate < 0) throw new FinancialCalculationException("La tasa no puede ser negativa en este producto inicial.");
        decimal years = YearFraction(from, to, dayCount);
        bool completePeriod = IsCompletePeriod(from, to, frequency);
        decimal periodsPerYear = frequency switch
        {
            PaymentFrequency.Daily => 365,
            PaymentFrequency.Weekly => 52,
            PaymentFrequency.Every15Days or PaymentFrequency.SemiMonthly => 24,
            PaymentFrequency.Monthly => 12,
            PaymentFrequency.Quarterly => 4,
            PaymentFrequency.SemiAnnual => 2,
            PaymentFrequency.Annual => 1,
            _ => throw new FinancialCalculationException("Periodicidad no admitida.")
        };
        try
        {
            return rateKind switch
            {
                RateKind.AnnualSimple => rate * years,
                RateKind.PeriodicSimple => completePeriod ? rate : rate * periodsPerYear * years,
                RateKind.EffectiveAnnual => DecimalMath.Pow(1 + rate, years) - 1,
                RateKind.PeriodicEffective => completePeriod ? rate : DecimalMath.Pow(1 + rate, periodsPerYear * years) - 1,
                RateKind.NominalAnnual => DecimalMath.Pow(1 + rate / periodsPerYear, periodsPerYear * years) - 1,
                _ => throw new FinancialCalculationException("Tipo de tasa no admitido.")
            };
        }
        catch (OverflowException)
        {
            throw new FinancialCalculationException("La tasa y el plazo exceden la precisión decimal admitida.");
        }
    }

    public static decimal YearFraction(DateOnly from, DateOnly to, DayCountConvention convention)
    {
        if (to <= from) throw new FinancialCalculationException("El fin del periodo debe ser posterior al inicio.");
        int days = to.DayNumber - from.DayNumber;
        return convention switch
        {
            DayCountConvention.Actual365Fixed => days / 365m,
            DayCountConvention.Actual360 => days / 360m,
            DayCountConvention.ActualActual => ActualActual(from, to),
            _ => throw new FinancialCalculationException("Convención de días no admitida.")
        };
    }

    private static bool IsCompletePeriod(DateOnly from, DateOnly to, PaymentFrequency frequency)
    {
        try
        {
            return frequency switch
            {
                PaymentFrequency.Daily => from.AddDays(1) == to,
                PaymentFrequency.Weekly => from.AddDays(7) == to,
                PaymentFrequency.Every15Days => from.AddDays(15) == to,
                PaymentFrequency.SemiMonthly => from.Day <= 15
                    ? to == new DateOnly(from.Year, from.Month, DateTime.DaysInMonth(from.Year, from.Month))
                    : to.Year == from.AddMonths(1).Year && to.Month == from.AddMonths(1).Month && to.Day <= 15,
                PaymentFrequency.Monthly => CompleteMonths(from, to, 1),
                PaymentFrequency.Quarterly => CompleteMonths(from, to, 3),
                PaymentFrequency.SemiAnnual => CompleteMonths(from, to, 6),
                PaymentFrequency.Annual => CompleteMonths(from, to, 12),
                _ => false
            };
        }
        catch (ArgumentOutOfRangeException) { return false; }
    }

    private static bool CompleteMonths(DateOnly from, DateOnly to, int months)
    {
        if (from.AddMonths(months) == to) return true;
        bool fromEnd = from.Day == DateTime.DaysInMonth(from.Year, from.Month);
        bool toEnd = to.Day == DateTime.DaysInMonth(to.Year, to.Month);
        return fromEnd && toEnd && from.AddMonths(months).Year == to.Year && from.AddMonths(months).Month == to.Month;
    }

    private static decimal ActualActual(DateOnly from, DateOnly to)
    {
        decimal sum = 0;
        DateOnly start = from;
        while (start < to)
        {
            DateOnly nextYear = start.Year == 9999 ? DateOnly.MaxValue : new DateOnly(start.Year + 1, 1, 1);
            DateOnly end = nextYear < to ? nextYear : to;
            sum += (end.DayNumber - start.DayNumber) / (DateTime.IsLeapYear(start.Year) ? 366m : 365m);
            start = end;
        }
        return sum;
    }
}
