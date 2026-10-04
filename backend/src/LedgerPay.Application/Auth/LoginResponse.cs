namespace LedgerPay.Application.Auth;

public sealed record LoginResponse(
    string AccessToken,
    string TokenType,
    DateTime ExpiresAt,
    string FullName,
    IReadOnlyList<string> Roles,
    string? WalletNumber);
