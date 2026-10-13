namespace Ziklo.FinancialEngine;

internal static class DecimalMath
{
    private const decimal Tolerance = 0.000000000000000000000001m;

    public static decimal Pow(decimal value, decimal exponent)
    {
        if (value <= 0) throw new FinancialCalculationException("La base de una potencia financiera debe ser positiva.");
        if (exponent == 0) return 1;
        if (value == 1) return 1;
        try { return Exp(exponent * Log(value)); }
        catch (OverflowException) { throw new FinancialCalculationException("La tasa y el plazo exceden la precisión decimal admitida."); }
    }

    private static decimal Log(decimal value)
    {
        // 2 * atanh((x-1)/(x+1)); converges quickly for the rate factors used here.
        decimal z = (value - 1) / (value + 1);
        decimal zSquared = z * z;
        decimal power = z;
        decimal sum = 0;
        for (int n = 1; n < 2048; n += 2)
        {
            decimal term = power / n;
            sum += term;
            if (Abs(term) < Tolerance) return 2 * sum;
            power *= zSquared;
        }
        throw new FinancialCalculationException("No convergió la conversión decimal de la tasa.");
    }

    private static decimal Exp(decimal value)
    {
        if (value < 0) return 1 / Exp(-value);
        decimal term = 1;
        decimal sum = 1;
        for (int n = 1; n < 2048; n++)
        {
            term = checked(term * value / n);
            sum = checked(sum + term);
            if (Abs(term) < Tolerance) return sum;
        }
        throw new FinancialCalculationException("No convergió el factor decimal de la tasa.");
    }

    private static decimal Abs(decimal value) => value < 0 ? -value : value;
}
