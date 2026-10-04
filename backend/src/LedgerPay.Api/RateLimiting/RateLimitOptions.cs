namespace LedgerPay.Api.RateLimiting;

// How many requests one caller may make in one window. The numbers are settings, so a test or a host can change them.
public sealed class RateLimitRule
{
    public int PermitLimit { get; set; }

    public int WindowSeconds { get; set; }
}

public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimits";
    private const int LongestWindowSeconds = 86_400;

    // Sign-in and register, counted for each address.
    public RateLimitRule Auth { get; set; } = new() { PermitLimit = 10, WindowSeconds = 60 };

    // Refresh and sign out, counted for each address. Every page load refreshes, so the limit is looser than for sign-in.
    public RateLimitRule Refresh { get; set; } = new() { PermitLimit = 30, WindowSeconds = 60 };

    // Wallet lookup, counted for each customer. A lookup can be used to scan for wallets.
    public RateLimitRule Lookup { get; set; } = new() { PermitLimit = 30, WindowSeconds = 60 };

    // Quote, transfer and top-up, counted for each signed-in user.
    public RateLimitRule Money { get; set; } = new() { PermitLimit = 30, WindowSeconds = 60 };

    // Called when the app starts, so a limit of zero cannot lock everyone out without a clear message.
    public void EnsureValid()
    {
        Check(nameof(Auth), Auth);
        Check(nameof(Refresh), Refresh);
        Check(nameof(Lookup), Lookup);
        Check(nameof(Money), Money);
    }

    private static void Check(string name, RateLimitRule rule)
    {
        if (rule.PermitLimit < 1)
        {
            throw new InvalidOperationException($"{SectionName}:{name}:{nameof(RateLimitRule.PermitLimit)} must be at least 1.");
        }

        if (rule.WindowSeconds is < 1 or > LongestWindowSeconds)
        {
            throw new InvalidOperationException($"{SectionName}:{name}:{nameof(RateLimitRule.WindowSeconds)} must be from 1 to {LongestWindowSeconds}.");
        }
    }
}
