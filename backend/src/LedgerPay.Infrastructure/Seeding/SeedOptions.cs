using LedgerPay.Domain.Rules;

namespace LedgerPay.Infrastructure.Seeding;

public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    public string AdminPassword { get; set; } = string.Empty;
    public string OperatorPassword { get; set; } = string.Empty;
    public string CustomerPassword { get; set; } = string.Empty;

    // No defaults on purpose: a missing password must stop the seed, never fall back to a known one.
    public void EnsureComplete()
    {
        Require(AdminPassword, $"{SectionName}:{nameof(AdminPassword)}");
        Require(OperatorPassword, $"{SectionName}:{nameof(OperatorPassword)}");
        Require(CustomerPassword, $"{SectionName}:{nameof(CustomerPassword)}");
    }

    private static void Require(string value, string key)
    {
        if (!PasswordPolicy.IsValid(value))
        {
            throw new InvalidOperationException(
                $"{key} is missing or breaks the password policy ({PasswordPolicy.MinimumLength} to {PasswordPolicy.MaximumLength} " +
                "characters with upper, lower, digit and symbol). Set it in user-secrets or the environment.");
        }
    }
}
