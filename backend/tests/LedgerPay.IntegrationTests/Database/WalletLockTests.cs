using LedgerPay.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace LedgerPay.IntegrationTests.Database;

public class WalletLockTests(SqlServerFixture sql)
{
    [Fact]
    public async Task Locking_a_wallet_returns_the_current_row_even_when_the_context_already_tracks_it()
    {
        await using var setup = sql.NewContext();
        var (_, wallet, _) = await TestData.AddCustomerAsync(setup);
        await TestData.FundAsync(setup, wallet, 1000.00m);
        await using var db = sql.NewContext();
        var stale = await db.Wallets.SingleAsync(w => w.Id == wallet.Id, TestContext.Current.CancellationToken);
        Assert.Equal(1000.00m, stale.Balance);
        await using (var other = sql.NewContext())
        {
            await TestData.FundAsync(other, wallet, 500.00m);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var locked = await db.LockWalletAsync(wallet.Id, TestContext.Current.CancellationToken);

        Assert.Equal(1500.00m, locked!.Balance);
    }
}
