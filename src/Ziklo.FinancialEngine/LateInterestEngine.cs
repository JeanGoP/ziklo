namespace Ziklo.FinancialEngine;

public enum GraceMode { Prospective, Retroactive }

public sealed record LateInterestRule(
    decimal AnnualRate, int GraceDays, GraceMode GraceMode,
    DayCountConvention DayCount, int CurrencyDecimals = 2,
    decimal? MaximumAmount = null);

public sealed record LateBasePoint(DateOnly EffectiveDate, decimal OutstandingBase);

public sealed record LateInterestSegment(
    DateOnly From, DateOnly To, int Days, decimal Base, decimal UnroundedInterest);

public sealed record LateInterestResult(
    DateOnly DueDate, DateOnly CutoffDate, int EligibleDays,
    decimal CumulativeInterest, decimal AlreadyPosted,
    decimal IncrementToPost, IReadOnlyList<LateInterestSegment> Segments);

public sealed class LateInterestEngine
{
    public LateInterestResult CalculateLateInterest(
        DateOnly dueDate, DateOnly cutoffDate, IReadOnlyList<LateBasePoint> baseTimeline,
        LateInterestRule rule, decimal alreadyPosted = 0)
    {
        if (rule.AnnualRate < 0 || rule.GraceDays < 0 || rule.CurrencyDecimals is < 0 or > 4 ||
            rule.MaximumAmount < 0 || alreadyPosted < 0)
            throw new FinancialCalculationException("La política de mora contiene valores no válidos.");
        if (!Enum.IsDefined(rule.GraceMode) || !Enum.IsDefined(rule.DayCount))
            throw new FinancialCalculationException("La política de mora contiene una convención no válida.");
        if (baseTimeline.Count == 0 || baseTimeline.Any(x => x.OutstandingBase < 0) ||
            baseTimeline.Select(x => x.EffectiveDate).Distinct().Count() != baseTimeline.Count ||
            baseTimeline.All(x => x.EffectiveDate > dueDate))
            throw new FinancialCalculationException("La historia de la base de mora no es válida.");
        if (cutoffDate <= dueDate)
            return Empty(dueDate, cutoffDate, alreadyPosted);

        DateOnly graceEnd;
        try { graceEnd = dueDate.AddDays(rule.GraceDays); }
        catch (ArgumentOutOfRangeException)
        {
            throw new FinancialCalculationException("El periodo de gracia excede las fechas admitidas.");
        }
        if (cutoffDate <= graceEnd)
            return Empty(dueDate, cutoffDate, alreadyPosted);
        DateOnly start = rule.GraceMode == GraceMode.Prospective ? graceEnd : dueDate;

        var points = baseTimeline.OrderBy(x => x.EffectiveDate).ToList();
        decimal currentBase = points.Last(x => x.EffectiveDate <= start).OutstandingBase;
        DateOnly cursor = start;
        decimal total = 0;
        var segments = new List<LateInterestSegment>();
        foreach (var point in points.Where(x => x.EffectiveDate > start && x.EffectiveDate < cutoffDate))
        {
            AddSegment(point.EffectiveDate);
            currentBase = point.OutstandingBase;
        }
        AddSegment(cutoffDate);

        decimal capped = rule.MaximumAmount.HasValue ? Math.Min(total, rule.MaximumAmount.Value) : total;
        decimal cumulative = Math.Round(capped, rule.CurrencyDecimals, MidpointRounding.AwayFromZero);
        if (alreadyPosted > cumulative)
            throw new FinancialCalculationException("La mora ya causada supera el cálculo acumulado; requiere ajuste autorizado.");
        return new LateInterestResult(dueDate, cutoffDate, cutoffDate.DayNumber - start.DayNumber,
            cumulative, alreadyPosted, cumulative - alreadyPosted, segments);

        void AddSegment(DateOnly end)
        {
            if (end <= cursor) return;
            int days = end.DayNumber - cursor.DayNumber;
            decimal amount = currentBase * rule.AnnualRate *
                InterestEngine.YearFraction(cursor, end, rule.DayCount);
            segments.Add(new LateInterestSegment(cursor, end, days, currentBase, amount));
            total += amount;
            cursor = end;
        }
    }

    private static LateInterestResult Empty(DateOnly due, DateOnly cutoff, decimal alreadyPosted)
    {
        if (alreadyPosted > 0)
            throw new FinancialCalculationException("Existe mora causada antes de comenzar el periodo elegible.");
        return new LateInterestResult(due, cutoff, 0, 0, 0, 0, []);
    }
}
