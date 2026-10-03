using LedgerPay.Domain.Entities;
using LedgerPay.Domain.Rules;

namespace LedgerPay.UnitTests.Rules;

public class LedgerPostingsTests
{
    private static readonly Guid Sender = Guid.NewGuid();
    private static readonly Guid Receiver = Guid.NewGuid();
    private static readonly Guid FeeRevenue = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 10, 3, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Transfer_debits_the_sender_for_amount_plus_fee_and_credits_the_receiver_and_the_fee_account()
    {
        var entries = LedgerPostings.ForTransfer(Sender, Receiver, FeeRevenue, 1000.00m, 10.00m, Now);

        Assert.Equal(3, entries.Count);
        var debit = Assert.Single(entries, entry => entry.Debit > 0);
        Assert.Equal((Sender, 1010.00m), (debit.LedgerAccountId, debit.Debit));
        Assert.Equal(1000.00m, entries.Single(entry => entry.LedgerAccountId == Receiver).Credit);
        Assert.Equal(10.00m, entries.Single(entry => entry.LedgerAccountId == FeeRevenue).Credit);
    }

    [Fact]
    public void Transfer_postings_balance()
    {
        var entries = LedgerPostings.ForTransfer(Sender, Receiver, FeeRevenue, 12450.00m, 62.25m, Now);

        Assert.Equal(entries.Sum(entry => entry.Debit), entries.Sum(entry => entry.Credit));
    }

    [Fact]
    public void Transfer_without_a_fee_posts_no_empty_fee_entry()
    {
        var entries = LedgerPostings.ForTransfer(Sender, Receiver, FeeRevenue, 500.00m, 0m, Now);

        Assert.Equal(2, entries.Count);
        Assert.All(entries, entry => Assert.True(entry.Debit > 0 ^ entry.Credit > 0));
    }

    [Fact]
    public void Every_entry_carries_the_posting_time()
    {
        var entries = LedgerPostings.ForTransfer(Sender, Receiver, FeeRevenue, 1000.00m, 10.00m, Now);

        Assert.All(entries, entry => Assert.Equal(Now, entry.CreatedAt));
    }

    [Fact]
    public void Unbalanced_entries_are_refused()
    {
        var entries = new List<LedgerEntry>
        {
            new() { Debit = 100.00m },
            new() { Credit = 99.99m }
        };

        var error = Assert.Throws<InvalidOperationException>(() => LedgerPostings.EnsureBalanced(entries));

        Assert.Contains("100.00", error.Message);
        Assert.Contains("99.99", error.Message);
    }

    [Fact]
    public void Balanced_entries_pass_the_check()
    {
        var entries = LedgerPostings.ForTransfer(Sender, Receiver, FeeRevenue, 1000.00m, 10.00m, Now);

        LedgerPostings.EnsureBalanced(entries);
    }

    [Fact]
    public void Top_up_debits_the_settlement_float_and_credits_the_wallet()
    {
        var entries = LedgerPostings.ForTopUp(Sender, Receiver, 2500.00m, Now);

        Assert.Equal(2, entries.Count);
        var debit = Assert.Single(entries, entry => entry.Debit > 0);
        var credit = Assert.Single(entries, entry => entry.Credit > 0);
        Assert.Equal((Sender, 2500.00m), (debit.LedgerAccountId, debit.Debit));
        Assert.Equal((Receiver, 2500.00m), (credit.LedgerAccountId, credit.Credit));
    }

    [Fact]
    public void Top_up_postings_balance_and_carry_the_posting_time()
    {
        var entries = LedgerPostings.ForTopUp(Sender, Receiver, 12450.00m, Now);

        LedgerPostings.EnsureBalanced(entries);
        Assert.Equal(12450.00m, entries.Sum(entry => entry.Credit));
        Assert.All(entries, entry => Assert.Equal(Now, entry.CreatedAt));
    }
}
