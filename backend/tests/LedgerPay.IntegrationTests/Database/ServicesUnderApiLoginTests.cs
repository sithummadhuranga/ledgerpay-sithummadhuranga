using LedgerPay.Application.Common;
using LedgerPay.Application.Transfers;
using LedgerPay.Domain.Constants;
using LedgerPay.Infrastructure.Persistence;
using LedgerPay.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace LedgerPay.IntegrationTests.Database;

// The services run as the limited login in production, so they are tried as that login here: the row locks,
// the idempotency key update and the inserts all need rights it has to be granted.
public class ServicesUnderApiLoginTests(SqlServerFixture sql)
{
    private static readonly RequestInfo Caller = new("203.0.113.50", "corr-api-login-test");

    private AppDbContext ApiContext() => SqlServerFixture.NewContext(sql.ApiConnectionString);

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
