namespace LedgerPay.Domain.Entities;

// One refresh token of one sign-in. Every use replaces it with a new row of the same family, so a family is one
// session and its newest unrevoked row is the token the browser holds now. Only the hash of the token is stored.
public sealed class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid FamilyId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime SessionStartedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public Guid? ReplacedById { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}
