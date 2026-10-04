namespace LedgerPay.Infrastructure.Security;

// The claim names as they are written in the token. The bearer setup keeps them as they are, so code reads these.
public static class JwtClaimNames
{
    public const string Subject = "sub";
    public const string Role = "role";
}
