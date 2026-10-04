namespace LedgerPay.Domain.Entities;

public sealed class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public int FailedLoginCount { get; set; }
    public DateTime? LockoutEnd { get; set; }
    public DateTime CreatedAt { get; set; }

    // Set when an admin has restricted the account. A restricted account cannot sign in, and its sessions are ended.
    public DateTime? RestrictedAt { get; set; }
    public string? RestrictedReason { get; set; }
    public Guid? RestrictedByUserId { get; set; }

    public List<UserRole> UserRoles { get; set; } = [];
    public Wallet? Wallet { get; set; }
}
