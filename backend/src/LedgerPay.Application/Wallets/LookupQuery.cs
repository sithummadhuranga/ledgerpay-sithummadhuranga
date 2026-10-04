namespace LedgerPay.Application.Wallets;

// Exactly one of the two is set. The validator checks that.
public sealed class LookupQuery
{
    public string? WalletNumber { get; set; }

    public string? Phone { get; set; }
}
