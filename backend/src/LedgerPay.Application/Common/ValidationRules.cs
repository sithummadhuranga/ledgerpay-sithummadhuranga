using FluentValidation;

namespace LedgerPay.Application.Common;

// Rules from the assignment that more than one request uses. The patterns use [0-9] and \A...\z,
// because \d also matches digits from other scripts and $ lets a trailing newline through.
public static class ValidationRules
{
    // The largest value a DECIMAL(18,2) column holds. The transfer maximum is a setting and is checked later,
    // this only keeps an absurd value from reaching the database as an error.
    public const decimal LargestAmount = 9_999_999_999_999_999.99m;

    public static IRuleBuilderOptions<T, decimal> MoneyAmount<T>(this IRuleBuilder<T, decimal> rule) =>
        rule.GreaterThan(0).WithMessage("Amount must be above 0.")
            .Must(amount => decimal.Round(amount, 2) == amount).WithMessage("Amount can have at most 2 decimal places.")
            .LessThanOrEqualTo(LargestAmount).WithMessage("Amount is too large.");

    public static IRuleBuilderOptions<T, string?> WalletNumber<T>(this IRuleBuilder<T, string?> rule) =>
        rule.NotNull().Matches(@"\A[0-9]{12}\z").WithMessage("Wallet number must be 12 digits.");

    public static IRuleBuilderOptions<T, string> BankReference<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotNull().Matches(@"\A[A-Za-z0-9]{6,40}\z").WithMessage("Bank reference must be 6 to 40 letters and digits.");

    public const int MaximumNoteLength = 140;

    public static IRuleBuilderOptions<T, string?> Note<T>(this IRuleBuilder<T, string?> rule) =>
        rule.MaximumLength(MaximumNoteLength).WithMessage($"Note can have at most {MaximumNoteLength} characters.");

    public static IRuleBuilderOptions<T, string?> MobileNumber<T>(this IRuleBuilder<T, string?> rule) =>
        rule.NotNull().Matches(@"\A\+947[0-9]{8}\z").WithMessage("Mobile number must look like +94771234567.");
}
