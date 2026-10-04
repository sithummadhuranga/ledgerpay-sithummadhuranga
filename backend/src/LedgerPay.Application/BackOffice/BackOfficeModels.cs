using LedgerPay.Domain.Enums;

namespace LedgerPay.Application.BackOffice;

// What staff see. Unlike what a customer sees, names are whole and the email and mobile number are there,
// because finding a person is the job. Internal ids are still left out: a wallet number is the handle of a person.

public sealed record UserSummary(
    string WalletNumber,
    string FullName,
    string Email,
    string Phone,
    decimal Balance,
    WalletStatus WalletStatus,
    bool Locked,
    DateTime CreatedAt);

public sealed record UserDetail(
    string WalletNumber,
    string FullName,
    string Email,
    string Phone,
    decimal Balance,
    WalletStatus WalletStatus,
    string? StatusReason,
    DateTime? StatusChangedAt,
    string? StatusChangedBy,
    bool Locked,
    DateTime? LockedUntil,
    int FailedLoginCount,
    DateTime CreatedAt,
    IReadOnlyList<StaffTransactionItem> RecentTransactions);

public sealed record StaffTransactionItem(
    string Reference,
    TransactionType Type,
    TransactionStatus Status,
    decimal Amount,
    decimal Fee,
    string? FailureCode,
    string? SenderWalletNumber,
    string? SenderName,
    string? ReceiverWalletNumber,
    string? ReceiverName,
    string? RequestedReceiver,
    string? BankReference,
    string? Note,
    DateTime CreatedAt);

public sealed record AuditItem(
    DateTime CreatedAt,
    string Action,
    string EntityType,
    string? EntityReference,
    string? ActorName,
    string? ActorEmail,
    string? IpAddress,
    string? CorrelationId,
    string? Details);
