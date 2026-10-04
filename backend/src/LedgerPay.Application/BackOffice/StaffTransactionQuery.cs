using LedgerPay.Domain.Enums;

namespace LedgerPay.Application.BackOffice;

// From and To are whole days in UTC, and both are included, as in a customer's history.
public sealed class StaffTransactionQuery
{
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;

    public TransactionType? Type { get; set; }

    public TransactionStatus? Status { get; set; }

    // Transactions that sent from or into this wallet.
    public string? WalletNumber { get; set; }

    public DateOnly? From { get; set; }

    public DateOnly? To { get; set; }
}
