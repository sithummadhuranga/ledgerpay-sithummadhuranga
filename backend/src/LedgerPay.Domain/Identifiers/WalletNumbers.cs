namespace LedgerPay.Domain.Identifiers;

public static class WalletNumbers
{
    // Plain digits only: char.IsDigit would also accept digits from other scripts.
    public static bool IsValid(string? value) =>
        value is { Length: WalletNumberGenerator.Length } && value.All(character => character is >= '0' and <= '9');
}
