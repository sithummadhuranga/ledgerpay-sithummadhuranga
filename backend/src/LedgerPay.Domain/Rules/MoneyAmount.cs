namespace LedgerPay.Domain.Rules;

public static class MoneyAmount
{
    // The largest value a DECIMAL(18,2) column holds. The transfer maximum is a setting and is checked later.
    public const decimal Largest = 9_999_999_999_999_999.99m;

    public static bool IsValid(decimal amount) => amount > 0 && decimal.Round(amount, 2) == amount && amount <= Largest;
}
