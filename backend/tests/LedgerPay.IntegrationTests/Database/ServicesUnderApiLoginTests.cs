using LedgerPay.Application.Admin;
using LedgerPay.Application.Common;
using LedgerPay.Application.TopUps;
using LedgerPay.Application.Transfers;
using LedgerPay.Domain.Constants;
using LedgerPay.Domain.Enums;
using LedgerPay.Infrastructure.Persistence;
using LedgerPay.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace LedgerPay.IntegrationTests.Database;

// The services run as the limited login in production, so they are tried as that login here: the row locks,
// the application lock, the idempotency key update and the inserts all need rights it has to be granted.
public class ServicesUnderApiLoginTests(SqlServerFixture sql)
{
    private static readonly RequestInfo Caller = new("203.0.113.50", "corr-api-login-test");

    private AppDbContext ApiContext() => SqlServerFixture.NewContext(sql.ApiConnectionString);

    [Fact]
    public async Task A_top_up_a_transfer_and_a_freeze_all_work_as_the_limited_login()
    {
        await using var setup = sql.NewContext();
        var (backOffice, _, _) = await TestData.AddCustomerAsync(setup);
        var (sender, senderWallet, _) = await TestData.AddCustomerAsync(setup);
        var (_, receiverWallet, _) = await TestData.AddCustomerAsync(setup);

        await using var topUpContext = ApiContext();
        var topUp = await TestServices.TopUps(topUpContext).TopUpAsync(
            backOffice.Id, TestServices.NewKey(), new TopUpRequest(senderWallet.WalletNumber, 5000.00m, TestServices.NewBankReference(), null),
            Caller, TestContext.Current.CancellationToken);
        await using var transferContext = ApiContext();
        var transfer = await TestServices.Transfers(transferContext).TransferAsync(
            sender.Id, TestServices.NewKey(), new TransferRequest(receiverWallet.WalletNumber, null, 1000.00m, null),
            Caller, TestContext.Current.CancellationToken);
        await using var freezeContext = ApiContext();
        var freeze = await TestServices.WalletStatus(freezeContext).SetStatusAsync(
            backOffice.Id, receiverWallet.WalletNumber, new WalletStatusRequest(WalletStatus.Frozen, "Suspected fraud"),
            Caller, TestContext.Current.CancellationToken);

        Assert.True(topUp.Succeeded);
        Assert.True(transfer.Succeeded);
        Assert.True(freeze.Succeeded);
        await using var check = sql.NewContext();
        Assert.Equal(3990.00m, (await check.Wallets.AsNoTracking().SingleAsync(w => w.Id == senderWallet.Id, TestContext.Current.CancellationToken)).Balance);
    }

    [Fact]
    public async Task A_replay_and_a_rejection_work_as_the_limited_login()
    {
        await using var setup = sql.NewContext();
        var (sender, senderWallet, _) = await TestData.AddCustomerAsync(setup);
        var (_, receiverWallet, _) = await TestData.AddCustomerAsync(setup);
        await TestData.FundAsync(setup, senderWallet, 500.00m);
        var key = TestServices.NewKey();
        var request = new TransferRequest(receiverWallet.WalletNumber, null, 1000.00m, null);

        await using var firstContext = ApiContext();
        var first = await TestServices.Transfers(firstContext).TransferAsync(
            sender.Id, key, request, Caller, TestContext.Current.CancellationToken);
        await using var secondContext = ApiContext();
        var second = await TestServices.Transfers(secondContext).TransferAsync(
            sender.Id, key, request, Caller, TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.InsufficientFunds, first.ErrorCode);
        Assert.Equal(ErrorCodes.InsufficientFunds, second.ErrorCode);
        Assert.True(second.Replayed);
    }
}
