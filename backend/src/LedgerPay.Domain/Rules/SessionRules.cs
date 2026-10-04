namespace LedgerPay.Domain.Rules;

public static class SessionRules
{
    // A token that is not used for this long stops working.
    public static readonly TimeSpan IdleLifetime = TimeSpan.FromDays(7);

    // A session ends this long after the sign-in, however often it was used.
    public static readonly TimeSpan MaxLifetime = TimeSpan.FromDays(30);

    // A token that was replaced a moment ago may still arrive, because two tabs or a retry sent it together.
    // Inside this window it gets an access token and nothing else. After it, the token coming back means a copy exists.
    public static readonly TimeSpan ReuseGrace = TimeSpan.FromSeconds(10);

    // 32 random bytes written as base64url without padding.
    public const int TokenBytes = 32;
    public const int TokenLength = 43;

    public static DateTime ExpiresAt(DateTime now, DateTime sessionStartedAt)
    {
        var idle = now + IdleLifetime;
        var max = sessionStartedAt + MaxLifetime;
        return idle < max ? idle : max;
    }

    // Checked before the database is asked, so a header of any other shape costs nothing.
    public static bool IsWellFormed(string? token) =>
        token is { Length: TokenLength } && token.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');
}
