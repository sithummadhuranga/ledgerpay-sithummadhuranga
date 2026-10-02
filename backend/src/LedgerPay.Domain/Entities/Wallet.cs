using LedgerPay.Domain.Enums;
using LedgerPay.Domain.Rules;

namespace LedgerPay.Domain.Entities;

public sealed class Wallet
{
    public Guid Id { get; set; }
    public string WalletNumber { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public decimal Balance { get; set; }
    public WalletStatus Status { get; set; }
    public string? StatusReason { get; set; }
    public Guid? StatusChangedByUserId { get; set; }
    public DateTime? StatusChangedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public User User { get; set; } = null!;

    public static Wallet Open(User user, DateTime now) => new()
    {
        User = user,
        WalletNumber = WalletNumberGenerator.Next(),
        Balance = 0m,
        Status = WalletStatus.Active,
        CreatedAt = now
    };
}
