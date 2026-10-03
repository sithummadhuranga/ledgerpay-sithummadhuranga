using System.Globalization;
using LedgerPay.Domain.Entities;

namespace LedgerPay.Domain.Rules;

public static class LedgerPostings
{
    // Dr sender (amount + fee), Cr receiver (amount), Cr fee revenue (fee).
    public static List<LedgerEntry> ForTransfer(
        Guid senderAccountId,
        Guid receiverAccountId,
        Guid feeRevenueAccountId,
        decimal amount,
        decimal fee,
        DateTime now)
    {
        var entries = new List<LedgerEntry>
        {
            new() { LedgerAccountId = senderAccountId, Debit = amount + fee, CreatedAt = now },
            new() { LedgerAccountId = receiverAccountId, Credit = amount, CreatedAt = now }
        };

        // The table requires exactly one side above zero, so a zero fee posts nothing.
        if (fee > 0)
        {
            entries.Add(new LedgerEntry { LedgerAccountId = feeRevenueAccountId, Credit = fee, CreatedAt = now });
        }

        return entries;
    }

    // Dr settlement float (amount), Cr the customer wallet (amount). A top-up has no fee.
    public static List<LedgerEntry> ForTopUp(Guid settlementAccountId, Guid walletAccountId, decimal amount, DateTime now) =>
    [
        new LedgerEntry { LedgerAccountId = settlementAccountId, Debit = amount, CreatedAt = now },
        new LedgerEntry { LedgerAccountId = walletAccountId, Credit = amount, CreatedAt = now }
    ];

    // A failure here is a bug in the posting code, never bad input, so it throws.
    public static void EnsureBalanced(IEnumerable<LedgerEntry> entries)
    {
        var list = entries.ToList();
        var debits = list.Sum(entry => entry.Debit);
        var credits = list.Sum(entry => entry.Credit);

        if (debits != credits)
        {
            throw new InvalidOperationException(
                $"Ledger entries are not balanced: debits {debits.ToString("0.00", CultureInfo.InvariantCulture)}, " +
                $"credits {credits.ToString("0.00", CultureInfo.InvariantCulture)}.");
        }
    }
}
