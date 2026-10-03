namespace LedgerPay.Application.Transfers;

// Exactly one of the two recipient fields is set. The validator checks that.
public sealed record TransferRequest(string? RecipientWalletNumber, string? RecipientPhone, decimal Amount, string? Note);
