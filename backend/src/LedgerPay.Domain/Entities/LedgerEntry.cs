namespace LedgerPay.Domain.Entities;

public sealed class LedgerEntry
{
    public Guid Id { get; set; }

    // Append order, assigned by the database. Breaks ties when two entries share a timestamp.
    public long Sequence { get; set; }
    public Guid TransactionId { get; set; }
    public Guid LedgerAccountId { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public DateTime CreatedAt { get; set; }

    public Transaction Transaction { get; set; } = null!;
    public LedgerAccount LedgerAccount { get; set; } = null!;
}
