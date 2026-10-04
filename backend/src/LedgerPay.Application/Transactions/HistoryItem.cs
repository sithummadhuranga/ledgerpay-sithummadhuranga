using LedgerPay.Domain.Enums;

namespace LedgerPay.Application.Transactions;

// Amount is what moved between the two wallets. Fee is what the sender paid on top, so it is 0 for the receiver.
// CounterpartyName is masked, and empty for a top-up. A transfer that was refused is listed for the sender with
// Status Failed and its FailureCode. Nothing moved, so it has no BalanceAfter.
public sealed record HistoryItem(
    string Reference,
    TransactionType Type,
    TransactionDirection Direction,
    decimal Amount,
    decimal Fee,
    string? CounterpartyName,
    string? Note,
    TransactionStatus Status,
    DateTime CreatedAt,
    decimal? BalanceAfter,
    string? FailureCode);
