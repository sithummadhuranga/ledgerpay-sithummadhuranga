namespace LedgerPay.Application.Auth;

public sealed record SessionResponse(
    Guid Id,
    DateTime SignedInAt,
    DateTime LastActiveAt,
    string? IpAddress,
    string? UserAgent,
    bool Current);
