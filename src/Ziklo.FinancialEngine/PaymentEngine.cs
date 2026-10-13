namespace Ziklo.FinancialEngine;

public enum PaymentCategory
{
    Charges, LateInterest, OverdueCurrentInterest, OverduePrincipal,
    CurrentInterest, Principal
}

public enum ExcessHandling { Reject, Credit }

public sealed record PaymentBucket(
    Guid BucketId, Guid? InstallmentId, PaymentCategory Category,
    DateOnly DueDate, decimal Outstanding);

public sealed record PaymentPolicy(
    IReadOnlyList<PaymentCategory> Priority,
    ExcessHandling ExcessHandling,
    int CurrencyDecimals = 2);

public sealed record PaymentAllocation(
    Guid BucketId, Guid? InstallmentId, PaymentCategory Category, decimal Amount);

public sealed record PaymentApplication(
    decimal Received, decimal Applied, decimal Credit,
    IReadOnlyList<PaymentAllocation> Allocations,
    IReadOnlyList<PaymentBucket> RemainingBuckets);

public sealed record PayoffComponents(
    decimal Principal, decimal CurrentInterestPosted, decimal CurrentInterestUnposted,
    decimal LateInterestPosted, decimal LateInterestUnposted,
    decimal Charges, decimal Credit);

public sealed record PayoffResult(
    PayoffComponents Components, decimal Total);

public sealed class PaymentEngine
{
    public static PaymentPolicy DefaultPolicy { get; } = new(
        [PaymentCategory.Charges, PaymentCategory.LateInterest,
         PaymentCategory.OverdueCurrentInterest, PaymentCategory.OverduePrincipal,
         PaymentCategory.CurrentInterest, PaymentCategory.Principal],
        ExcessHandling.Credit);

    public PaymentApplication ApplyPayment(decimal received,
        IReadOnlyList<PaymentBucket> buckets, PaymentPolicy policy)
    {
        if (received <= 0) throw new FinancialCalculationException("El pago recibido debe ser positivo.");
        if (policy.CurrencyDecimals is < 0 or > 4 ||
            Math.Round(received, policy.CurrencyDecimals) != received ||
            buckets.Any(x => Math.Round(x.Outstanding, policy.CurrencyDecimals) != x.Outstanding))
            throw new FinancialCalculationException("El pago o la deuda excede la escala monetaria configurada.");
        if (buckets.Any(x => x.Outstanding < 0))
            throw new FinancialCalculationException("Una deuda elegible no puede ser negativa.");
        if (buckets.Select(x => x.BucketId).Distinct().Count() != buckets.Count)
            throw new FinancialCalculationException("Las deudas elegibles tienen identificadores repetidos.");
        if (policy.Priority.Count != Enum.GetValues<PaymentCategory>().Length ||
            policy.Priority.Distinct().Count() != policy.Priority.Count ||
            Enum.GetValues<PaymentCategory>().Except(policy.Priority).Any())
            throw new FinancialCalculationException("La política de pago debe ordenar cada componente una vez.");

        decimal remaining = received;
        var allocations = new List<PaymentAllocation>();
        var remainingById = buckets.ToDictionary(x => x.BucketId, x => x.Outstanding);
        foreach (PaymentCategory category in policy.Priority)
        {
            foreach (PaymentBucket bucket in buckets.Where(x => x.Category == category)
                         .OrderBy(x => x.DueDate).ThenBy(x => x.BucketId))
            {
                if (remaining == 0) break;
                decimal applied = Math.Min(remaining, remainingById[bucket.BucketId]);
                if (applied == 0) continue;
                remainingById[bucket.BucketId] -= applied;
                remaining -= applied;
                allocations.Add(new PaymentAllocation(bucket.BucketId, bucket.InstallmentId,
                    bucket.Category, applied));
            }
        }
        if (remaining > 0 && policy.ExcessHandling == ExcessHandling.Reject)
            throw new FinancialCalculationException("El pago supera la deuda elegible.");
        decimal appliedTotal = received - remaining;
        var debts = buckets.Select(x => x with { Outstanding = remainingById[x.BucketId] }).ToList();
        if (allocations.Sum(x => x.Amount) != appliedTotal)
            throw new FinancialCalculationException("La aplicación del pago no concilia.");
        return new PaymentApplication(received, appliedTotal, remaining, allocations, debts);
    }

    public PayoffResult CalculatePayoff(PayoffComponents components)
    {
        if (components.Principal < 0 || components.CurrentInterestPosted < 0 ||
            components.CurrentInterestUnposted < 0 || components.LateInterestPosted < 0 ||
            components.LateInterestUnposted < 0 || components.Charges < 0 ||
            components.Credit < 0)
            throw new FinancialCalculationException("La liquidación contiene un componente negativo.");
        decimal total = components.Principal + components.CurrentInterestPosted +
            components.CurrentInterestUnposted + components.LateInterestPosted +
            components.LateInterestUnposted + components.Charges - components.Credit;
        if (total < 0) throw new FinancialCalculationException("El saldo a favor supera el valor de cancelación.");
        return new PayoffResult(components, total);
    }
}
