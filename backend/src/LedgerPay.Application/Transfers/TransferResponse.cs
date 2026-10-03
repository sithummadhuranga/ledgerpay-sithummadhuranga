namespace LedgerPay.Application.Transfers;

public sealed record TransferResponse(
    string Reference,
    decimal Amount,
    decimal Fee,
    decimal Total,
    decimal BalanceAfter,
    string RecipientWalletNumber,
    DateTime CreatedAt);
