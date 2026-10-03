namespace LedgerPay.Application.TopUps;

public sealed record TopUpRequest(string WalletNumber, decimal Amount, string BankReference, string? Note);
