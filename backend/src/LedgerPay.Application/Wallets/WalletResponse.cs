using LedgerPay.Domain.Enums;

namespace LedgerPay.Application.Wallets;

public sealed record WalletResponse(
    string WalletNumber,
    string HolderName,
    decimal Balance,
    decimal AvailableBalance,
    string Currency,
    WalletStatus Status);
