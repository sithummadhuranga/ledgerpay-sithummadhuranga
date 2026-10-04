using LedgerPay.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace LedgerPay.IntegrationTests.Database;

public class WalletStatementViewTests(SqlServerFixture sql)
{
    [Fact]
    public async Task Running_balance_follows_append_order_even_when_timestamps_tie()
    {
        await using var db = sql.NewContext();
        var (user, wallet, account) = await TestData.AddCustomerAsync(db);
        var sameInstant = new DateTime(2026, 10, 3, 9, 30, 0, DateTimeKind.Utc);

        var topUp = await TestData.AddTransactionAsync(db, user.Id);
        await TestData.AddEntryAsync(db, topUp.Id, account.Id, 0m, 500.00m, sameInstant);
        var transfer = await TestData.AddTransactionAsync(db, user.Id);
        await TestData.AddEntryAsync(db, transfer.Id, account.Id, 150.00m, 0m, sameInstant);
        var secondTopUp = await TestData.AddTransactionAsync(db, user.Id);
        await TestData.AddEntryAsync(db, secondTopUp.Id, account.Id, 0m, 20.00m, sameInstant);

        var rows = await db.Database
            .SqlQuery<StatementRow>($"SELECT Debit, Credit, BalanceAfter FROM vw_WalletStatement WHERE WalletId = {wallet.Id} ORDER BY Sequence")
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal([500.00m, 350.00m, 370.00m], rows.Select(row => row.BalanceAfter));
    }

    [Fact]
    public async Task Running_balance_follows_append_order_not_the_timestamp()
    {
        await using var db = sql.NewContext();
        var (user, wallet, account) = await TestData.AddCustomerAsync(db);
        var later = new DateTime(2026, 10, 3, 9, 30, 5, DateTimeKind.Utc);
        var earlier = new DateTime(2026, 10, 3, 9, 30, 0, DateTimeKind.Utc);

        var topUp = await TestData.AddTransactionAsync(db, user.Id);
        await TestData.AddEntryAsync(db, topUp.Id, account.Id, 0m, 500.00m, later);
        var transfer = await TestData.AddTransactionAsync(db, user.Id);
        await TestData.AddEntryAsync(db, transfer.Id, account.Id, 150.00m, 0m, earlier);

        var rows = await db.Database
            .SqlQuery<StatementRow>($"SELECT Debit, Credit, BalanceAfter FROM vw_WalletStatement WHERE WalletId = {wallet.Id} ORDER BY Sequence")
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal([500.00m, 350.00m], rows.Select(row => row.BalanceAfter));
    }

    [Fact]
    public async Task Running_balance_is_kept_apart_per_wallet()
    {
        await using var db = sql.NewContext();
        var (userA, walletA, accountA) = await TestData.AddCustomerAsync(db);
        var (userB, walletB, accountB) = await TestData.AddCustomerAsync(db);

        var first = await TestData.AddTransactionAsync(db, userA.Id);
        await TestData.AddEntryAsync(db, first.Id, accountA.Id, 0m, 300.00m);
        var second = await TestData.AddTransactionAsync(db, userB.Id);
        await TestData.AddEntryAsync(db, second.Id, accountB.Id, 0m, 70.00m);

        var rowsA = await db.Database
            .SqlQuery<StatementRow>($"SELECT Debit, Credit, BalanceAfter FROM vw_WalletStatement WHERE WalletId = {walletA.Id}")
            .ToListAsync(TestContext.Current.CancellationToken);
        var rowsB = await db.Database
            .SqlQuery<StatementRow>($"SELECT Debit, Credit, BalanceAfter FROM vw_WalletStatement WHERE WalletId = {walletB.Id}")
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(300.00m, Assert.Single(rowsA).BalanceAfter);
        Assert.Equal(70.00m, Assert.Single(rowsB).BalanceAfter);
    }

    [Fact]
    public async Task Filtering_by_date_keeps_the_running_balance_of_the_full_history()
    {
        await using var db = sql.NewContext();
        var (user, wallet, account) = await TestData.AddCustomerAsync(db);
        var early = new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc);
        var late = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);

        var first = await TestData.AddTransactionAsync(db, user.Id);
        await TestData.AddEntryAsync(db, first.Id, account.Id, 0m, 1000.00m, early);
        var second = await TestData.AddTransactionAsync(db, user.Id);
        await TestData.AddEntryAsync(db, second.Id, account.Id, 250.00m, 0m, late);

        var from = new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc);
        var rows = await db.Database
            .SqlQuery<StatementRow>($"SELECT Debit, Credit, BalanceAfter FROM vw_WalletStatement WHERE WalletId = {wallet.Id} AND CreatedAt >= {from}")
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(750.00m, Assert.Single(rows).BalanceAfter);
    }

    private sealed record StatementRow(decimal Debit, decimal Credit, decimal BalanceAfter);
}
