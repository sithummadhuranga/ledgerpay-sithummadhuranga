using LedgerPay.Domain.Enums;

namespace LedgerPay.Domain.Entities;

public sealed class Transaction
{
    public Guid Id { get; set; }
    public string Reference { get; set; } = string.Empty;
    public TransactionType Type { get; set; }
    public TransactionStatus Status { get; set; }
    public string? FailureCode { get; set; }
    public Guid? SenderWalletId { get; set; }
    public Guid? ReceiverWalletId { get; set; }

    // What the sender typed. Kept so a failed attempt to an unknown recipient still shows who it was for.
    public string? RequestedReceiver { get; set; }
    public decimal Amount { get; set; }
    public decimal Fee { get; set; }
    public string? Note { get; set; }
    public string? BankReference { get; set; }
    public Guid InitiatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }

    public Wallet? SenderWallet { get; set; }
    public Wallet? ReceiverWallet { get; set; }
    public List<LedgerEntry> Entries { get; set; } = [];
}
