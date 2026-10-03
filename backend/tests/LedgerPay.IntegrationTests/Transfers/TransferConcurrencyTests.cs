using LedgerPay.Application.Common;
using LedgerPay.Application.Settings;
using LedgerPay.Application.Transfers;
using LedgerPay.Domain.Constants;
using LedgerPay.Domain.Entities;
using LedgerPay.Domain.Enums;
using LedgerPay.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace LedgerPay.IntegrationTests.Transfers;

public class TransferConcurrencyTests(SqlServerFixture sql)
{
    private static readonly RequestInfo Caller = new("203.0.113.8", "corr-concurrency-test");

    private async Task<ServiceResult<TransferResponse>> SendAsync(Guid userId, Wallet receiver, decimal amount)
    {
        await using var db = sql.NewContext();
        var service = new TransferService(db, new LedgerSettingsProvider(db), TimeProvider.System);
        return await service.TransferAsync(
            userId, new TransferRequest(receiver.WalletNumber, null, amount, null), Caller, CancellationToken.None);
    }

    private async Task<(User User, Wallet Wallet)> CustomerAsync(decimal balance)
    {
        await using var db = sql.NewContext();
        var (user, wallet, _) = await TestData.AddCustomerAsync(db);
        if (balance > 0)
        {
            await TestData.FundAsync(db, wallet, balance);
        }

        return (user, wallet);
    }

    [Fact]
    public async Task Twenty_parallel_transfers_from_one_wallet_only_succeed_as_often_as_it_can_afford()
    {
        // Each transfer of 1000.00 costs 1010.00 with the fee, so 5050.00 covers exactly five of them.
        var (sender, senderWallet) = await CustomerAsync(5050.00m);
        var (_, receiverWallet) = await CustomerAsync(0m);

        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => SendAsync(sender.Id, receiverWallet, 1000.00m)));

        Assert.Equal(5, results.Count(result => result.Succeeded));
        Assert.All(results.Where(result => !result.Succeeded), result => Assert.Equal(ErrorCodes.InsufficientFunds, result.ErrorCode));
        await using var check = sql.NewContext();
        var senderStored = await check.Wallets.AsNoTracking().SingleAsync(w => w.Id == senderWallet.Id, TestContext.Current.CancellationToken);
        var receiverStored = await check.Wallets.AsNoTracking().SingleAsync(w => w.Id == receiverWallet.Id, TestContext.Current.CancellationToken);
        Assert.Equal(0m, senderStored.Balance);
        Assert.Equal(5000.00m, receiverStored.Balance);
        Assert.Equal(await TestData.LedgerBalanceAsync(check, senderWallet), senderStored.Balance);
        Assert.Equal(await TestData.LedgerBalanceAsync(check, receiverWallet), receiverStored.Balance);
        Assert.Equal(15, await check.Transactions.CountAsync(
            t => t.SenderWalletId == senderWallet.Id && t.Status == TransactionStatus.Failed, TestContext.Current.CancellationToken));
        Assert.Equal(15, await check.LedgerEntries.CountAsync(
            e => e.Transaction.SenderWalletId == senderWallet.Id && e.Transaction.Type == TransactionType.Transfer, TestContext.Current.CancellationToken));
        Assert.Equal(20, await check.AuditLogs.CountAsync(log => log.ActorUserId == sender.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Two_parallel_transfers_cannot_both_push_the_same_receiver_over_the_cap()
    {
        var (senderOne, walletOne) = await CustomerAsync(1000.00m);
        var (senderTwo, walletTwo) = await CustomerAsync(1000.00m);
        var (_, receiverWallet) = await CustomerAsync(1_999_000.00m);

        var results = await Task.WhenAll(
            SendAsync(senderOne.Id, receiverWallet, 600.00m),
            SendAsync(senderTwo.Id, receiverWallet, 600.00m));

        Assert.Equal(1, results.Count(result => result.Succeeded));
        Assert.Equal(ErrorCodes.ReceiverBalanceLimitExceeded, results.Single(result => !result.Succeeded).ErrorCode);
        await using var check = sql.NewContext();
        var receiverStored = await check.Wallets.AsNoTracking().SingleAsync(w => w.Id == receiverWallet.Id, TestContext.Current.CancellationToken);
        Assert.Equal(1_999_600.00m, receiverStored.Balance);
        Assert.Equal(await TestData.LedgerBalanceAsync(check, receiverWallet), receiverStored.Balance);
        Assert.Equal(2000.00m - 610.00m, await TestData.LedgerBalanceAsync(check, walletOne) + await TestData.LedgerBalanceAsync(check, walletTwo));
    }

    [Fact]
    public async Task Opposite_transfers_between_two_wallets_do_not_deadlock()
    {
        var (userA, walletA) = await CustomerAsync(100_000.00m);
        var (userB, walletB) = await CustomerAsync(100_000.00m);

        var sends = Enumerable.Range(0, 10).SelectMany(_ => new[]
        {
            SendAsync(userA.Id, walletB, 1000.00m),
            SendAsync(userB.Id, walletA, 1000.00m)
        });
        var results = await Task.WhenAll(sends);

        Assert.All(results, result => Assert.True(result.Succeeded));
        await using var check = sql.NewContext();
        var a = await check.Wallets.AsNoTracking().SingleAsync(w => w.Id == walletA.Id, TestContext.Current.CancellationToken);
        var b = await check.Wallets.AsNoTracking().SingleAsync(w => w.Id == walletB.Id, TestContext.Current.CancellationToken);
        Assert.Equal(100_000.00m - 10 * 10.00m, a.Balance);
        Assert.Equal(100_000.00m - 10 * 10.00m, b.Balance);
        Assert.Equal(await TestData.LedgerBalanceAsync(check, walletA), a.Balance);
        Assert.Equal(await TestData.LedgerBalanceAsync(check, walletB), b.Balance);
    }
}
