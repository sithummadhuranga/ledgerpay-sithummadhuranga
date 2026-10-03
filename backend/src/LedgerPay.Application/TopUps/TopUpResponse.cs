namespace LedgerPay.Application.TopUps;

public sealed record TopUpResponse(
    string Reference,
    string WalletNumber,
    decimal Amount,
    decimal BalanceAfter,
    string BankReference,
    DateTime CreatedAt);
