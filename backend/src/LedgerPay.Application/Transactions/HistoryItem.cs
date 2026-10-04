using LedgerPay.Domain.Enums;

namespace LedgerPay.Application.Transactions;

// Amount is what moved between the two wallets. Fee is what the sender paid on top, so it is 0 for the receiver.
// CounterpartyName is masked, and empty for a top-up.
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
    decimal BalanceAfter);
