namespace LedgerPay.Application.Auth;

public sealed record RegisterRequest(string FullName, string Email, string Phone, string Password);
