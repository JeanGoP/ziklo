namespace Ziklo.FinancialEngine;

public sealed class LoanSimulator
{
    private readonly InterestEngine _interest = new();

    public SimulationResult Simulate(SimulationRequest request)
    {
        try
        {
            Validate(request);
            var dates = BuildDates(request);
            var result = request.Method == AmortizationMethod.Bullet
                ? BuildBullet(request, dates)
                : BuildAmortizing(request, dates);

            if (result.Schedule.Any(x => x.ClosingPrincipal < 0 || x.Payment < 0))
                throw new FinancialCalculationException("El plan contiene un saldo o pago negativo.");
            if (result.Schedule[^1].ClosingPrincipal != 0)
                throw new FinancialCalculationException("El plan no cancela el capital al vencimiento.");
            return result;
        }
        catch (OverflowException)
        {
            throw new FinancialCalculationException("Los importes, la tasa o el plazo exceden la precisión decimal admitida.");
        }
    }

    private static void Validate(SimulationRequest request)
    {
        if (!Enum.IsDefined(request.RateKind) || !Enum.IsDefined(request.DayCount) ||
            !Enum.IsDefined(request.Frequency) || !Enum.IsDefined(request.Method))
            throw new FinancialCalculationException("Una de las políticas financieras no es válida.");
        if (request.Principal <= 0) throw new FinancialCalculationException("El capital debe ser mayor que cero.");
        if (request.Principal > 999999999999999m)
            throw new FinancialCalculationException("El capital excede el máximo monetario admitido.");
        if (request.Rate < 0) throw new FinancialCalculationException("La tasa no puede ser negativa en este producto inicial.");
        if (request.Installments is < 1 or > 600) throw new FinancialCalculationException("El número de periodos debe estar entre 1 y 600.");
        if (request.CurrencyDecimals is < 0 or > 4) throw new FinancialCalculationException("La escala monetaria debe estar entre 0 y 4 decimales.");
        if (request.FirstDueDate <= request.DisbursementDate)
            throw new FinancialCalculationException("El primer vencimiento debe ser posterior al desembolso.");
        if (request.InsurancePerInstallment < 0 || request.OtherChargePerInstallment < 0)
            throw new FinancialCalculationException("Los cargos no pueden ser negativos.");
        if (request.CapitalizeInterest && request.Method != AmortizationMethod.Bullet)
            throw new FinancialCalculationException("La capitalización explícita inicial solo está disponible para pago único.");
        if (request.Method == AmortizationMethod.Custom)
        {
            if (request.CustomPrincipal is null || request.CustomPrincipal.Count != request.Installments ||
                request.CustomPrincipal.Any(x => x < 0) || request.CustomPrincipal.Sum() != request.Principal)
                throw new FinancialCalculationException("El capital personalizado debe sumar exactamente el capital original.");
        }
    }

    private static IReadOnlyList<DateOnly> BuildDates(SimulationRequest request)
    {
        var dates = new List<DateOnly>(request.Installments);
        bool endOfMonth = request.FirstDueDate.Day == DateTime.DaysInMonth(request.FirstDueDate.Year, request.FirstDueDate.Month);
        for (int index = 0; index < request.Installments; index++)
        {
            DateOnly date;
            try
            {
                date = request.Frequency switch
                {
                    PaymentFrequency.Daily => request.FirstDueDate.AddDays(index),
                    PaymentFrequency.Weekly => request.FirstDueDate.AddDays(checked(index * 7)),
                    PaymentFrequency.Every15Days => request.FirstDueDate.AddDays(checked(index * 15)),
                    PaymentFrequency.SemiMonthly => SemiMonthlyDate(request.FirstDueDate, index),
                    PaymentFrequency.Monthly => AddMonths(request.FirstDueDate, index, endOfMonth),
                    PaymentFrequency.Quarterly => AddMonths(request.FirstDueDate, checked(index * 3), endOfMonth),
                    PaymentFrequency.SemiAnnual => AddMonths(request.FirstDueDate, checked(index * 6), endOfMonth),
                    PaymentFrequency.Annual => AddMonths(request.FirstDueDate, checked(index * 12), endOfMonth),
                    _ => throw new FinancialCalculationException("Periodicidad no admitida.")
                };
            }
            catch (ArgumentOutOfRangeException)
            {
                throw new FinancialCalculationException("El calendario excede las fechas admitidas.");
            }
            if (dates.Count > 0 && date <= dates[^1])
                throw new FinancialCalculationException("Las fechas de vencimiento deben ser crecientes.");
            dates.Add(date);
        }
        return dates;
    }

    private static DateOnly AddMonths(DateOnly anchor, int months, bool endOfMonth)
    {
        var date = anchor.AddMonths(months);
        return endOfMonth ? new DateOnly(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month)) : date;
    }

    private static DateOnly SemiMonthlyDate(DateOnly anchor, int index)
    {
        // Two fixed calendar dates: the anchor day in the first half and month end.
        if (anchor.Day > 15) throw new FinancialCalculationException("La primera fecha quincenal fija debe estar entre los días 1 y 15.");
        var month = anchor.AddMonths(index / 2);
        return index % 2 == 0
            ? new DateOnly(month.Year, month.Month, anchor.Day)
            : new DateOnly(month.Year, month.Month, DateTime.DaysInMonth(month.Year, month.Month));
    }

    private SimulationResult BuildAmortizing(SimulationRequest request, IReadOnlyList<DateOnly> dates)
    {
        decimal[] rates = new decimal[dates.Count];
        DateOnly previous = request.DisbursementDate;
        for (int k = 0; k < dates.Count; k++)
        {
            rates[k] = _interest.CalculateIntervalRate(request.Rate, request.RateKind,
                request.DayCount, request.Frequency, previous, dates[k]);
            previous = dates[k];
        }

        decimal fixedPayment = request.Method == AmortizationMethod.French
            ? Round(CalculateLevelPayment(request.Principal, rates), request.CurrencyDecimals)
            : 0;
        decimal fixedPrincipal = request.Method == AmortizationMethod.FixedPrincipal
            ? Round(request.Principal / request.Installments, request.CurrencyDecimals)
            : 0;
        var lines = new List<ScheduleLine>(dates.Count);
        decimal balance = request.Principal;
        for (int k = 0; k < dates.Count; k++)
        {
            decimal interest = Round(balance * rates[k], request.CurrencyDecimals);
            decimal principal = request.Method switch
            {
                AmortizationMethod.French => fixedPayment - interest,
                AmortizationMethod.FixedPrincipal => fixedPrincipal,
                AmortizationMethod.SimplePeriodic => k == dates.Count - 1 ? balance : 0,
                AmortizationMethod.Custom => request.CustomPrincipal![k],
                _ => throw new FinancialCalculationException("Sistema de amortización no admitido.")
            };
            if (k == dates.Count - 1 && request.Method is AmortizationMethod.French or AmortizationMethod.FixedPrincipal)
                principal = balance;
            if (principal < 0 || principal > balance)
                throw new FinancialCalculationException("La cuota no amortiza el capital de forma válida.");
            decimal closing = balance - principal;
            decimal payment = principal + interest + request.InsurancePerInstallment + request.OtherChargePerInstallment;
            lines.Add(new ScheduleLine(k + 1, dates[k], balance, principal, interest, 0,
                request.InsurancePerInstallment, request.OtherChargePerInstallment, payment, closing));
            balance = closing;
        }
        return Summarize(request.Principal, lines);
    }

    private SimulationResult BuildBullet(SimulationRequest request, IReadOnlyList<DateOnly> dates)
    {
        var lines = new List<ScheduleLine>();
        decimal balance = request.Principal;
        DateOnly previous = request.DisbursementDate;
        if (request.CapitalizeInterest)
        {
            for (int k = 0; k < dates.Count; k++)
            {
                decimal interest = Round(_interest.CalculateInterest(balance, request.Rate,
                    request.RateKind, request.DayCount, request.Frequency, previous, dates[k]),
                    request.CurrencyDecimals);
                bool last = k == dates.Count - 1;
                decimal capitalized = last ? 0 : interest;
                decimal principalPaid = last ? balance : 0;
                decimal insurance = last ? request.InsurancePerInstallment : 0;
                decimal charges = last ? request.OtherChargePerInstallment : 0;
                decimal payment = last ? principalPaid + interest + insurance + charges : 0;
                decimal closing = last ? 0 : balance + capitalized;
                lines.Add(new ScheduleLine(k + 1, dates[k], balance, principalPaid, interest,
                    capitalized, insurance, charges, payment, closing));
                balance = closing;
                previous = dates[k];
            }
        }
        else
        {
            DateOnly maturity = dates[^1];
            decimal rateSum = 0;
            foreach (DateOnly date in dates)
            {
                rateSum += _interest.CalculateIntervalRate(request.Rate, request.RateKind,
                    request.DayCount, request.Frequency, previous, date);
                previous = date;
            }
            decimal interest = Round(balance * rateSum, request.CurrencyDecimals);
            lines.Add(new ScheduleLine(1, maturity, balance, balance, interest, 0,
                request.InsurancePerInstallment, request.OtherChargePerInstallment,
                balance + interest + request.InsurancePerInstallment + request.OtherChargePerInstallment, 0));
        }
        return Summarize(request.Principal, lines);
    }

    private static SimulationResult Summarize(decimal originalPrincipal, IReadOnlyList<ScheduleLine> lines)
    {
        decimal totalPrincipal = lines.Sum(x => x.Principal);
        decimal totalInterest = lines.Sum(x => x.Interest);
        decimal insurance = lines.Sum(x => x.Insurance);
        decimal charges = lines.Sum(x => x.OtherCharges);
        decimal payable = lines.Sum(x => x.Payment);
        decimal estimated = lines.First(x => x.Payment > 0).Payment;
        if (payable != originalPrincipal + totalInterest + insurance + charges)
            throw new FinancialCalculationException("Los totales del plan no concilian.");
        return new SimulationResult(originalPrincipal, totalPrincipal, totalInterest, insurance, charges,
            payable, estimated, lines[^1].DueDate, lines);
    }

    private static decimal CalculateLevelPayment(decimal principal, IReadOnlyList<decimal> rates)
    {
        decimal discount = 1;
        decimal factorSum = 0;
        foreach (decimal rate in rates)
        {
            discount /= 1 + rate;
            factorSum += discount;
        }
        return principal / factorSum;
    }

    public static decimal YearFraction(DateOnly from, DateOnly to, DayCountConvention convention)
        => InterestEngine.YearFraction(from, to, convention);

    private static decimal Round(decimal value, int decimals) =>
        Math.Round(value, decimals, MidpointRounding.AwayFromZero);
}
