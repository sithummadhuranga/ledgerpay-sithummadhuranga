using LedgerPay.Domain.Enums;

namespace LedgerPay.Application.Admin;

public sealed record WalletStatusResponse(string WalletNumber, WalletStatus Status, string Reason, DateTime ChangedAt);
