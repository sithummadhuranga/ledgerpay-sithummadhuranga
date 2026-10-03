using LedgerPay.Domain.Enums;

namespace LedgerPay.Application.Admin;

public sealed record WalletStatusRequest(WalletStatus Status, string Reason);
