using LedgerPay.Domain.Constants;
using LedgerPay.Domain.Enums;

namespace LedgerPay.Domain.Entities;

public sealed class LedgerAccount
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public LedgerAccountType Type { get; set; }
    public Guid? WalletId { get; set; }

    public Wallet? Wallet { get; set; }

    // Every customer wallet has its own account, which is where the wallet's postings go.
    public static LedgerAccount ForWallet(Wallet wallet) => new()
    {
        Code = LedgerAccountCodes.WalletPrefix + wallet.WalletNumber,
        Name = "Wallet " + wallet.WalletNumber,
        Type = LedgerAccountType.Liability,
        Wallet = wallet
    };
}
