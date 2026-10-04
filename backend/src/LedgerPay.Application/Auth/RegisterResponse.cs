namespace LedgerPay.Application.Auth;

public sealed record RegisterResponse(string FullName, string Email, string Phone, string WalletNumber);
