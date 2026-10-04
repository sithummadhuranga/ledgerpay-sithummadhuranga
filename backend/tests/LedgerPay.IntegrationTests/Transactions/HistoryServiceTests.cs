using LedgerPay.Application.Common;
using LedgerPay.Application.Transactions;
using LedgerPay.Application.TopUps;
using LedgerPay.Application.Transfers;
using LedgerPay.Domain.Entities;
using LedgerPay.Domain.Enums;
using LedgerPay.IntegrationTests.Support;
using Microsoft.Extensions.Time.Testing;

namespace LedgerPay.IntegrationTests.Transactions;

public class HistoryServiceTests(SqlServerFixture sql)
{
    private static readonly RequestInfo Caller = new("203.0.113.70", "corr-history-test");

    private static FakeTimeProvider ClockAt(int day, int hour = 9) =>
        new(new DateTimeOffset(2026, 9, day, hour, 0, 0, TimeSpan.Zero));

    private async Task<User> CustomerAsync()
    {
        await using var db = sql.NewContext();
        var (user, _, _) = await TestData.AddCustomerAsync(db);
        return user;
    }

    private async Task TopUpAsync(User customer, decimal amount, TimeProvider clock)
    {
        await using var db = sql.NewContext();
        var wallet = db.Wallets.Single(candidate => candidate.UserId == customer.Id);
        var result = await TestServices.TopUps(db, clock).TopUpAsync(
            customer.Id, TestServices.NewKey(),
            new TopUpRequest(wallet.WalletNumber, amount, TestServices.NewBankReference(), null), Caller, CancellationToken.None);
        Assert.True(result.Succeeded);
    }

    private async Task<string> TransferAsync(User from, User to, decimal amount, TimeProvider clock)
    {
        await using var db = sql.NewContext();
        var receiver = db.Wallets.Single(candidate => candidate.UserId == to.Id);
        var result = await TestServices.Transfers(db, clock).TransferAsync(
            from.Id, TestServices.NewKey(),
            new TransferRequest(receiver.WalletNumber, null, amount, "for lunch"), Caller, CancellationToken.None);
        Assert.True(result.Succeeded, result.ErrorCode);
        return result.Value!.Reference;
    }

    private async Task<PagedResponse<HistoryItem>> HistoryAsync(User customer, HistoryQuery? query = null)
    {
        await using var db = sql.NewContext();
        var result = await new TransactionQueries(db).HistoryAsync(customer.Id, query ?? new HistoryQuery(), CancellationToken.None);
        Assert.True(result.Succeeded, result.ErrorCode);
        return result.Value!;
    }

    [Fact]
    public async Task The_newest_entry_comes_first_with_the_direction_the_fee_and_the_balance_after_it()
    {
        var sender = await CustomerAsync();
        var receiver = await CustomerAsync();
        await TopUpAsync(sender, 5000m, ClockAt(1));
        var reference = await TransferAsync(sender, receiver, 1000m, ClockAt(2));

        var history = await HistoryAsync(sender);

        Assert.Equal(2, history.TotalCount);
        var sent = history.Items[0];
        Assert.Equal(reference, sent.Reference);
        Assert.Equal(TransactionType.Transfer, sent.Type);
        Assert.Equal(TransactionDirection.Sent, sent.Direction);
        Assert.Equal(1000m, sent.Amount);
        Assert.Equal(10m, sent.Fee);
        Assert.Equal(3990m, sent.BalanceAfter);
        Assert.Equal("N*** P***", sent.CounterpartyName);
        Assert.Equal("for lunch", sent.Note);
        Assert.Equal(TransactionStatus.Completed, sent.Status);
        Assert.Equal(DateTimeKind.Utc, sent.CreatedAt.Kind);
        Assert.Equal(new DateTime(2026, 9, 2, 9, 0, 0, DateTimeKind.Utc), sent.CreatedAt);
    }

    [Fact]
    public async Task A_top_up_shows_as_received_with_no_fee_and_no_counterparty()
    {
        var customer = await CustomerAsync();
        await TopUpAsync(customer, 5000m, ClockAt(1));

        var item = (await HistoryAsync(customer)).Items.Single();

        Assert.Equal(TransactionType.TopUp, item.Type);
        Assert.Equal(TransactionDirection.Received, item.Direction);
        Assert.Equal(5000m, item.Amount);
        Assert.Equal(0m, item.Fee);
        Assert.Equal(5000m, item.BalanceAfter);
        Assert.Null(item.CounterpartyName);
    }

    [Fact]
    public async Task The_receiver_sees_the_amount_without_the_fee_and_a_masked_sender()
    {
        var sender = await CustomerAsync();
        var receiver = await CustomerAsync();
        await TopUpAsync(sender, 5000m, ClockAt(1));
        await TransferAsync(sender, receiver, 1000m, ClockAt(2));

        var item = (await HistoryAsync(receiver)).Items.Single();

        Assert.Equal(TransactionDirection.Received, item.Direction);
        Assert.Equal(1000m, item.Amount);
        Assert.Equal(0m, item.Fee);
        Assert.Equal(1000m, item.BalanceAfter);
        Assert.Equal("N*** P***", item.CounterpartyName);
    }

    [Fact]
    public async Task The_balance_after_each_entry_is_a_running_total_that_ends_at_the_wallet_balance()
    {
        var sender = await CustomerAsync();
        var receiver = await CustomerAsync();
        await TopUpAsync(sender, 5000m, ClockAt(1));
        await TransferAsync(sender, receiver, 1000m, ClockAt(2));
        await TransferAsync(sender, receiver, 200m, ClockAt(3));

        var history = await HistoryAsync(sender);

        Assert.Equal([5000m - 1010m - 210m, 5000m - 1010m, 5000m], history.Items.Select(item => item.BalanceAfter));
        await using var db = sql.NewContext();
        Assert.Equal(history.Items[0].BalanceAfter, db.Wallets.Single(wallet => wallet.UserId == sender.Id).Balance);
    }

    [Fact]
    public async Task Paging_cuts_the_list_and_counts_the_pages()
    {
        var customer = await CustomerAsync();
        for (var day = 1; day <= 5; day++)
        {
            await TopUpAsync(customer, 100m, ClockAt(day));
        }

        var first = await HistoryAsync(customer, new HistoryQuery { Page = 1, PageSize = 2 });
        var third = await HistoryAsync(customer, new HistoryQuery { Page = 3, PageSize = 2 });
        var beyond = await HistoryAsync(customer, new HistoryQuery { Page = 4, PageSize = 2 });

        Assert.Equal((5, 3, 2), (first.TotalCount, first.TotalPages, first.Items.Count));
        Assert.Single(third.Items);
        Assert.Equal(100m, third.Items[0].BalanceAfter);
        Assert.Empty(beyond.Items);
        Assert.Equal(5, beyond.TotalCount);
        var second = await HistoryAsync(customer, new HistoryQuery { Page = 2, PageSize = 2 });
        var references = first.Items.Concat(second.Items).Concat(third.Items).Select(item => item.Reference).ToList();
        Assert.Equal(5, references.Distinct().Count());
    }

    [Fact]
    public async Task The_date_filters_include_both_days_in_full_and_keep_the_running_balance_of_the_whole_wallet()
    {
        var customer = await CustomerAsync();
        await TopUpAsync(customer, 100m, ClockAt(1, 0));
        await TopUpAsync(customer, 200m, ClockAt(2, 0));
        await TopUpAsync(customer, 400m, ClockAt(2, 23));
        await TopUpAsync(customer, 800m, ClockAt(3, 0));

        var history = await HistoryAsync(customer, new HistoryQuery { From = new DateOnly(2026, 9, 2), To = new DateOnly(2026, 9, 2) });

        Assert.Equal(2, history.TotalCount);
        Assert.Equal([400m, 200m], history.Items.Select(item => item.Amount));
        Assert.Equal([700m, 300m], history.Items.Select(item => item.BalanceAfter));
    }

    [Fact]
    public async Task Only_a_from_date_or_only_a_to_date_cuts_one_side()
    {
        var customer = await CustomerAsync();
        await TopUpAsync(customer, 100m, ClockAt(1));
        await TopUpAsync(customer, 200m, ClockAt(2));
        await TopUpAsync(customer, 400m, ClockAt(3));

        var from = await HistoryAsync(customer, new HistoryQuery { From = new DateOnly(2026, 9, 2) });
        var to = await HistoryAsync(customer, new HistoryQuery { To = new DateOnly(2026, 9, 2) });

        Assert.Equal([400m, 200m], from.Items.Select(item => item.Amount));
        Assert.Equal([200m, 100m], to.Items.Select(item => item.Amount));
    }

    [Fact]
    public async Task A_rejected_transfer_is_not_in_the_history()
    {
        var sender = await CustomerAsync();
        var receiver = await CustomerAsync();
        await TopUpAsync(sender, 150m, ClockAt(1));
        await using (var db = sql.NewContext())
        {
            var wallet = db.Wallets.Single(candidate => candidate.UserId == receiver.Id);
            var result = await TestServices.Transfers(db, ClockAt(2)).TransferAsync(
                sender.Id, TestServices.NewKey(), new TransferRequest(wallet.WalletNumber, null, 1000m, null), Caller, CancellationToken.None);
            Assert.False(result.Succeeded);
        }

        var history = await HistoryAsync(sender);

        Assert.Single(history.Items);
        Assert.Empty((await HistoryAsync(receiver)).Items);
    }

    [Fact]
    public async Task Another_customers_entries_never_appear()
    {
        var first = await CustomerAsync();
        var second = await CustomerAsync();
        await TopUpAsync(first, 100m, ClockAt(1));
        await TopUpAsync(second, 900m, ClockAt(1));

        var history = await HistoryAsync(first);

        Assert.Equal(100m, history.Items.Single().Amount);
    }

    [Fact]
    public async Task A_wallet_with_no_entries_gives_an_empty_page_with_no_pages()
    {
        var customer = await CustomerAsync();

        var history = await HistoryAsync(customer);

        Assert.Empty(history.Items);
        Assert.Equal((0, 0, 1, 20), (history.TotalCount, history.TotalPages, history.Page, history.PageSize));
    }

    [Fact]
    public async Task A_user_without_a_wallet_is_not_found()
    {
        await using var db = sql.NewContext();

        var result = await new TransactionQueries(db).HistoryAsync(Guid.NewGuid(), new HistoryQuery(), CancellationToken.None);

        Assert.Equal("WALLET_NOT_FOUND", result.ErrorCode);
    }
}
