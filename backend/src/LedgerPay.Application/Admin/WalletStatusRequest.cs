using LedgerPay.Domain.Enums;

namespace LedgerPay.Application.Admin;

// Status is nullable because Active is the zero value. A body that leaves it out would otherwise unfreeze the wallet.
public sealed record WalletStatusRequest(WalletStatus? Status, string Reason);
