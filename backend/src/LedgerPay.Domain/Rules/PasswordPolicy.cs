namespace LedgerPay.Domain.Rules;

public static class PasswordPolicy
{
    public const int MinimumLength = 10;

    // Bounds the work a hash can be made to do.
    public const int MaximumLength = 128;

    public static bool IsValid(string password) =>
        password.Length is >= MinimumLength and <= MaximumLength
        && password.Any(char.IsUpper)
        && password.Any(char.IsLower)
        && password.Any(char.IsDigit)
        && password.Any(character => !char.IsLetterOrDigit(character));
}
