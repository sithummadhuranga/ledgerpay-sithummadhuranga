using System.Text.RegularExpressions;
using FluentValidation;
using LedgerPay.Domain.Rules;

namespace LedgerPay.Application.Common;

// Rules from the assignment that more than one request uses. The patterns use [0-9] and \A...\z,
// because \d also matches digits from other scripts and $ lets a trailing newline through.
public static class ValidationRules
{
    // Keeps an absurd value from reaching the database as an error. The transfer maximum is a setting, checked later.
    public const decimal LargestAmount = LedgerPay.Domain.Rules.MoneyAmount.Largest;

    public static IRuleBuilderOptions<T, decimal> MoneyAmount<T>(this IRuleBuilder<T, decimal> rule) =>
        rule.GreaterThan(0).WithMessage("Amount must be above 0.")
            .Must(amount => decimal.Round(amount, 2) == amount).WithMessage("Amount can have at most 2 decimal places.")
            .LessThanOrEqualTo(LargestAmount).WithMessage("Amount is too large.");

    public static IRuleBuilderOptions<T, string?> WalletNumber<T>(this IRuleBuilder<T, string?> rule) =>
        rule.NotNull().Matches(@"\A[0-9]{12}\z").WithMessage("Wallet number must be 12 digits.");

    public static IRuleBuilderOptions<T, string> BankReference<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotNull().Matches(@"\A[A-Za-z0-9]{6,40}\z").WithMessage("Bank reference must be 6 to 40 letters and digits.");

    // A word starts with a letter and goes on with letters and marks (Sinhala vowel signs are marks). Sinhala
    // conjuncts such as ශ්‍රී hold a zero width joiner, which is allowed only when a letter follows it.
    private const string NameWord = @"\p{L}(?:[\p{L}\p{M}]|\u200D(?=\p{L}))*";

    // Words joined by a single space, hyphen or apostrophe.
    private static readonly Regex NamePattern = Pattern($@"\A{NameWord}(?:[ '\-]{NameWord})*\z");

    // Dots go between parts, never first, last or doubled. The local part is at most 64 characters and the last
    // label of the domain is letters only. It is not the full RFC grammar.
    private static readonly Regex EmailPattern = Pattern(
        @"\A(?=[^@]{1,64}@)[A-Za-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[A-Za-z0-9!#$%&'*+/=?^_`{|}~-]+)*@(?:[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?\.)+[A-Za-z]{2,}\z");

    private static Regex Pattern(string pattern) => new(pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    // Spaces around the name are cut off before the check, and the service cuts them off before it stores the name.
    public static IRuleBuilderOptions<T, string> FullName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotNull()
            .Must(name => name.Trim().Length is >= 2 and <= 100).WithMessage("Full name must be 2 to 100 characters.")
            .Must(name => NamePattern.IsMatch(name.Trim()))
            .WithMessage("Full name can hold letters, with single spaces, hyphens or apostrophes between them.");

    // ASCII only, because the column is varchar and a letter such as î could otherwise be matched against an i.
    // Spaces around the address are cut off before the check, and the services cut them off before they use it.
    public static IRuleBuilderOptions<T, string> EmailAddress<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotNull()
            .Must(email => email.Trim().Length <= 254).WithMessage("Email can have at most 254 characters.")
            .Must(email => EmailPattern.IsMatch(email.Trim()))
            .WithMessage("Enter a valid email address.");

    public static IRuleBuilderOptions<T, string> NewPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotNull()
            .Must(password => password is not null && PasswordPolicy.IsValid(password))
            .WithMessage($"Password needs {PasswordPolicy.MinimumLength} to {PasswordPolicy.MaximumLength} characters with upper case, lower case, a digit and a symbol.");

    public const int MaximumNoteLength = 140;

    public static IRuleBuilderOptions<T, string?> Note<T>(this IRuleBuilder<T, string?> rule) =>
        rule.MaximumLength(MaximumNoteLength).WithMessage($"Note can have at most {MaximumNoteLength} characters.");

    public static IRuleBuilderOptions<T, string?> MobileNumber<T>(this IRuleBuilder<T, string?> rule) =>
        rule.NotNull().Matches(@"\A\+947[0-9]{8}\z").WithMessage("Mobile number must look like +94771234567.");
}
