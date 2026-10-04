using LedgerPay.Application.Common;
using LedgerPay.Application.Settings;
using LedgerPay.Application.Transfers;
using LedgerPay.Domain.Constants;
using LedgerPay.Infrastructure.Persistence;
using LedgerPay.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace LedgerPay.IntegrationTests.Transfers;

// These tests change SystemSettings, so each one works in a database of its own.
public class SettingsDrivenTests(SqlServerFixture sql)
{
    private static readonly RequestInfo Caller = new("203.0.113.9", "corr-settings-test");

    private static TransferService NewService(AppDbContext db) => TestServices.Transfers(db);

    private static async Task SetAsync(string connectionString, string key, decimal value)
    {
        await using var db = SqlServerFixture.NewContext(connectionString);
        var setting = await db.SystemSettings.SingleAsync(s => s.Key == key, TestContext.Current.CancellationToken);
        setting.Value = value;
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Quote_follows_the_fee_percent_stored_in_system_settings()
    {
        var connectionString = await sql.CreateIsolatedDatabaseAsync();
        await SetAsync(connectionString, SettingKeys.FeePercent, 1.00m);
        await using var db = SqlServerFixture.NewContext(connectionString);

        var result = await NewService(db).QuoteAsync(5000.00m, TestContext.Current.CancellationToken);

        Assert.Equal(50.00m, result.Value!.Fee);
    }

    [Fact]
    public async Task Transfer_limits_follow_the_values_stored_in_system_settings()
    {
        var connectionString = await sql.CreateIsolatedDatabaseAsync();
        await SetAsync(connectionString, SettingKeys.TransferMinimum, 500.00m);
        await SetAsync(connectionString, SettingKeys.TransferMaximum, 1000.00m);
        await using var setup = SqlServerFixture.NewContext(connectionString);
        var (sender, senderWallet, _) = await TestData.AddCustomerAsync(setup);
        var (_, receiverWallet, _) = await TestData.AddCustomerAsync(setup);
        await TestData.FundAsync(setup, senderWallet, 5000.00m);
        await using var db = SqlServerFixture.NewContext(connectionString);
        var service = NewService(db);

        var below = await service.TransferAsync(
            sender.Id, TestServices.NewKey(), new TransferRequest(receiverWallet.WalletNumber, null, 400.00m, null), Caller, TestContext.Current.CancellationToken);
        var above = await service.TransferAsync(
            sender.Id, TestServices.NewKey(), new TransferRequest(receiverWallet.WalletNumber, null, 1200.00m, null), Caller, TestContext.Current.CancellationToken);
        var inside = await service.TransferAsync(
            sender.Id, TestServices.NewKey(), new TransferRequest(receiverWallet.WalletNumber, null, 800.00m, null), Caller, TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.AmountBelowMinimum, below.ErrorCode);
        Assert.Equal(ErrorCodes.AmountAboveMaximum, above.ErrorCode);
        Assert.True(inside.Succeeded);
    }

    [Fact]
    public async Task Fee_minimum_and_maximum_follow_the_values_stored_in_system_settings()
    {
        var connectionString = await sql.CreateIsolatedDatabaseAsync();
        await SetAsync(connectionString, SettingKeys.FeeMinimum, 20.00m);
        await SetAsync(connectionString, SettingKeys.FeeMaximum, 30.00m);
        await using var db = SqlServerFixture.NewContext(connectionString);
        var service = NewService(db);

        var small = await service.QuoteAsync(1000.00m, TestContext.Current.CancellationToken);
        var large = await service.QuoteAsync(100_000.00m, TestContext.Current.CancellationToken);

        Assert.Equal(20.00m, small.Value!.Fee);
        Assert.Equal(30.00m, large.Value!.Fee);
    }
}
