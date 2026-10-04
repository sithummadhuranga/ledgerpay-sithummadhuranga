namespace LedgerPay.Infrastructure.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public const int MinimumKeyBytes = 32;
    public const int MaximumAccessTokenMinutes = 15;

    // Base64 of at least 32 random bytes. It comes from user-secrets or the environment, never from a file in Git.
    public string SigningKey { get; set; } = string.Empty;

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = MaximumAccessTokenMinutes;

    public byte[] SigningKeyBytes() => Convert.FromBase64String(SigningKey);

    // Called when the app starts, so a missing or weak setting stops it with a clear message.
    public void EnsureValid()
    {
        if (string.IsNullOrWhiteSpace(SigningKey))
        {
            throw Invalid($"{SectionName}:{nameof(SigningKey)} is missing. Set it in user-secrets or the environment as base64 of at least {MinimumKeyBytes} random bytes.");
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(SigningKey);
        }
        catch (FormatException)
        {
            throw Invalid($"{SectionName}:{nameof(SigningKey)} is not valid base64.");
        }

        if (bytes.Length < MinimumKeyBytes)
        {
            throw Invalid($"{SectionName}:{nameof(SigningKey)} must decode to at least {MinimumKeyBytes} bytes.");
        }

        if (string.IsNullOrWhiteSpace(Issuer))
        {
            throw Invalid($"{SectionName}:{nameof(Issuer)} is missing.");
        }

        if (string.IsNullOrWhiteSpace(Audience))
        {
            throw Invalid($"{SectionName}:{nameof(Audience)} is missing.");
        }

        if (AccessTokenMinutes is < 1 or > MaximumAccessTokenMinutes)
        {
            throw Invalid($"{SectionName}:{nameof(AccessTokenMinutes)} must be from 1 to {MaximumAccessTokenMinutes}.");
        }
    }

    private static InvalidOperationException Invalid(string message) => new(message);
}
