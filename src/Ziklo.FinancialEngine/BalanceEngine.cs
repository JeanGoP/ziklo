namespace Ziklo.FinancialEngine;

public enum FinancialComponent
{
    Principal, CurrentInterest, LateInterest, Insurance, Charge, Credit
}

public sealed record FinancialDelta(long Sequence, FinancialComponent Component, decimal Amount);

public sealed record OutstandingBalance(
    decimal Principal, decimal CurrentInterest, decimal LateInterest,
    decimal Insurance, decimal Charges, decimal Credit, decimal NetBalance,
    decimal AmountDue, long LastSequence);

public sealed class BalanceEngine
{
    public OutstandingBalance CalculateOutstandingBalance(IReadOnlyList<FinancialDelta> movements)
    {
        decimal principal = 0, currentInterest = 0, lateInterest = 0;
        decimal insurance = 0, charges = 0, credit = 0;
        long expected = 1;
        foreach (var movement in movements.OrderBy(x => x.Sequence))
        {
            if (movement.Sequence != expected++ || movement.Amount == 0 ||
                !Enum.IsDefined(movement.Component))
                throw new FinancialCalculationException("El libro contiene una secuencia o movimiento no válido.");
            try
            {
                switch (movement.Component)
                {
                    case FinancialComponent.Principal: principal += movement.Amount; break;
                    case FinancialComponent.CurrentInterest: currentInterest += movement.Amount; break;
                    case FinancialComponent.LateInterest: lateInterest += movement.Amount; break;
                    case FinancialComponent.Insurance: insurance += movement.Amount; break;
                    case FinancialComponent.Charge: charges += movement.Amount; break;
                    case FinancialComponent.Credit: credit += movement.Amount; break;
                }
            }
            catch (OverflowException)
            {
                throw new FinancialCalculationException("El libro excede la precisión decimal admitida.");
            }
            if (principal < 0 || currentInterest < 0 || lateInterest < 0 ||
                insurance < 0 || charges < 0 || credit < 0)
                throw new FinancialCalculationException("Un movimiento deja negativo un componente del saldo.");
        }
        decimal net = principal + currentInterest + lateInterest + insurance + charges - credit;
        return new OutstandingBalance(principal, currentInterest, lateInterest, insurance,
            charges, credit, net, Math.Max(0, net), expected - 1);
    }
}
