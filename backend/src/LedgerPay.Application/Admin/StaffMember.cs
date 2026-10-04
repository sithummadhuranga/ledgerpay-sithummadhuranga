namespace LedgerPay.Application.Admin;

public sealed record StaffMember(
    string FullName,
    string Email,
    string Role,
    bool Restricted,
    DateTime? RestrictedAt,
    string? RestrictedReason,
    string? RestrictedBy,
    DateTime CreatedAt);
