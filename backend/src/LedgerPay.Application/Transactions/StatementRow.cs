namespace LedgerPay.Application.Transactions;

// One line of the wallet statement view joined to its transaction. Type and Status are the stored text,
// because the raw query does not run the enum conversions.
public sealed class StatementRow
{
    public long Sequence { get; set; }

    public DateTime CreatedAt { get; set; }

    public decimal Debit { get; set; }

    public decimal BalanceAfter { get; set; }

    public string Reference { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public decimal Fee { get; set; }

    public string? Note { get; set; }

    public string? CounterpartyName { get; set; }
}
