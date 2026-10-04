namespace LedgerPay.Application.Transactions;

// One line of a wallet's history. Most come from the wallet statement view joined to their transaction. The
// transfers the wallet's holder sent and had refused come with them, because they have no entries in the view.
// Type and Status are the stored text, because the raw query does not run the enum conversions.
public sealed class StatementRow
{
    // Null for a refused transfer, which was never appended to the ledger.
    public long? Sequence { get; set; }

    public DateTime CreatedAt { get; set; }

    public bool Sent { get; set; }

    // Null for a refused transfer: no money moved, so there is no balance after it.
    public decimal? BalanceAfter { get; set; }

    public string Reference { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string? FailureCode { get; set; }

    public decimal Amount { get; set; }

    public decimal Fee { get; set; }

    public string? Note { get; set; }

    public string? CounterpartyName { get; set; }
}
