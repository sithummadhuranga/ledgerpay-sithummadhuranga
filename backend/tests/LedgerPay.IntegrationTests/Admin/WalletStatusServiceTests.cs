using LedgerPay.Application.Admin;
using LedgerPay.Application.Common;
using LedgerPay.Application.Transfers;
using LedgerPay.Domain.Constants;
using LedgerPay.Domain.Entities;
using LedgerPay.Domain.Enums;
using LedgerPay.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace LedgerPay.IntegrationTests.Admin;

public class WalletStatusServiceTests(SqlServerFixture sql)
{
    private static readonly RequestInfo Caller = new("203.0.113.40", "corr-status-test");

    private async Task<(User User, Wallet Wallet)> CustomerAsync(decimal balance = 0m)
    {
        await using var db = sql.NewContext();
        var (user, wallet, _) = await TestData.AddCustomerAsync(db);
        if (balance > 0)
        {
            await TestData.FundAsync(db, wallet, balance);
        }

        return (user, wallet);
    }

    private async Task<ServiceResult<WalletStatusResponse>> SetAsync(
        Guid actorId, string walletNumber, WalletStatus status, string reason, TimeProvider? clock = null)
    {
        await using var db = sql.NewContext();
        return await TestServices.WalletStatus(db, clock).SetStatusAsync(
            actorId, walletNumber, new WalletStatusRequest(status, reason), Caller, CancellationToken.None);
    }

    private async Task<Wallet> StoredAsync(Wallet wallet)
    {
        await using var check = sql.NewContext();
        return await check.Wallets.AsNoTracking().SingleAsync(w => w.Id == wallet.Id, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Freezing_a_wallet_stores_the_status_the_reason_the_actor_and_the_time()
    {
        var (backOffice, _) = await CustomerAsync();
        var (_, wallet) = await CustomerAsync();
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 4, 10, 0, 0, TimeSpan.Zero));

        var result = await SetAsync(backOffice.Id, wallet.WalletNumber, WalletStatus.Frozen, "Suspected fraud, ticket 4821", clock);

        Assert.True(result.Succeeded);
        Assert.Equal((wallet.WalletNumber, WalletStatus.Frozen, "Suspected fraud, ticket 4821"),
            (result.Value!.WalletNumber, result.Value.Status, result.Value.Reason));
        var stored = await StoredAsync(wallet);
        Assert.Equal(WalletStatus.Frozen, stored.Status);
        Assert.Equal("Suspected fraud, ticket 4821", stored.StatusReason);
        Assert.Equal(backOffice.Id, stored.StatusChangedByUserId);
        Assert.Equal(new DateTime(2026, 10, 4, 10, 0, 0, DateTimeKind.Utc), stored.StatusChangedAt);
    }

    [Fact]
    public async Task Freezing_writes_an_audit_entry_with_the_reason()
    {
        var (backOffice, _) = await CustomerAsync();
        var (_, wallet) = await CustomerAsync();

        await SetAsync(backOffice.Id, wallet.WalletNumber, WalletStatus.Frozen, "Suspected fraud");

        await using var check = sql.NewContext();
        var audit = await check.AuditLogs.AsNoTracking().SingleAsync(
            log => log.EntityReference == wallet.WalletNumber && log.Action == AuditActions.WalletFrozen,
            TestContext.Current.CancellationToken);
        Assert.Equal(AuditEntityTypes.Wallet, audit.EntityType);
        Assert.Equal(backOffice.Id, audit.ActorUserId);
        Assert.Equal("Suspected fraud", audit.Details);
        Assert.Equal("203.0.113.40", audit.IpAddress);
    }

    [Fact]
    public async Task Unfreezing_a_wallet_makes_it_active_again_and_is_audited()
    {
        var (backOffice, _) = await CustomerAsync();
        var (_, wallet) = await CustomerAsync();
        await SetAsync(backOffice.Id, wallet.WalletNumber, WalletStatus.Frozen, "Suspected fraud");

        var result = await SetAsync(backOffice.Id, wallet.WalletNumber, WalletStatus.Active, "Customer verified by phone");

        Assert.True(result.Succeeded);
        var stored = await StoredAsync(wallet);
        Assert.Equal(WalletStatus.Active, stored.Status);
        Assert.Equal("Customer verified by phone", stored.StatusReason);
        await using var check = sql.NewContext();
        Assert.Equal(1, await check.AuditLogs.CountAsync(
            log => log.EntityReference == wallet.WalletNumber && log.Action == AuditActions.WalletUnfrozen,
            TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(WalletStatus.Active)]
    [InlineData(WalletStatus.Frozen)]
    public async Task Setting_a_wallet_to_the_state_it_is_already_in_is_a_conflict_and_changes_nothing(WalletStatus status)
    {
        var (backOffice, _) = await CustomerAsync();
        var (_, wallet) = await CustomerAsync();
        if (status == WalletStatus.Frozen)
        {
            await SetAsync(backOffice.Id, wallet.WalletNumber, WalletStatus.Frozen, "First reason");
        }

        var result = await SetAsync(backOffice.Id, wallet.WalletNumber, status, "Second reason");

        Assert.Equal(ErrorCodes.WalletAlreadyInState, result.ErrorCode);
        var stored = await StoredAsync(wallet);
        Assert.Equal(status, stored.Status);
        Assert.NotEqual("Second reason", stored.StatusReason);
    }

    [Fact]
    public async Task An_unknown_wallet_number_is_not_found()
    {
        var (backOffice, _) = await CustomerAsync();

        var result = await SetAsync(backOffice.Id, "999999999997", WalletStatus.Frozen, "Suspected fraud");

        Assert.Equal(ErrorCodes.WalletNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task A_frozen_wallet_can_neither_send_nor_receive_until_it_is_unfrozen()
    {
        var (backOffice, _) = await CustomerAsync();
        var (sender, senderWallet) = await CustomerAsync(5000.00m);
        var (otherSender, _) = await CustomerAsync(5000.00m);
        await SetAsync(backOffice.Id, senderWallet.WalletNumber, WalletStatus.Frozen, "Suspected fraud");
        var (_, receiverWallet) = await CustomerAsync();

        await using var db = sql.NewContext();
        var service = TestServices.Transfers(db);
        var cannotSend = await service.TransferAsync(
            sender.Id, TestServices.NewKey(), new TransferRequest(receiverWallet.WalletNumber, null, 500.00m, null), Caller,
            TestContext.Current.CancellationToken);
        var cannotReceive = await service.TransferAsync(
            otherSender.Id, TestServices.NewKey(), new TransferRequest(senderWallet.WalletNumber, null, 500.00m, null), Caller,
            TestContext.Current.CancellationToken);
        await SetAsync(backOffice.Id, senderWallet.WalletNumber, WalletStatus.Active, "Customer verified");
        await using var after = sql.NewContext();
        var canSend = await TestServices.Transfers(after).TransferAsync(
            sender.Id, TestServices.NewKey(), new TransferRequest(receiverWallet.WalletNumber, null, 500.00m, null), Caller,
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.WalletFrozen, cannotSend.ErrorCode);
        Assert.Equal(ErrorCodes.WalletFrozen, cannotReceive.ErrorCode);
        Assert.True(canSend.Succeeded);
    }

    [Fact]
    public async Task A_freeze_waits_for_a_transaction_that_holds_the_wallet_lock()
    {
        var (backOffice, _) = await CustomerAsync();
        var (_, wallet) = await CustomerAsync(1000.00m);
        await using var holder = sql.NewContext();
        await using var transaction = await holder.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await holder.LockWalletAsync(wallet.Id, TestContext.Current.CancellationToken);

        var freeze = SetAsync(backOffice.Id, wallet.WalletNumber, WalletStatus.Frozen, "Suspected fraud");
        await Task.Delay(750, TestContext.Current.CancellationToken);
        var finishedWhileLocked = freeze.IsCompleted;
        await transaction.CommitAsync(TestContext.Current.CancellationToken);
        var result = await freeze;

        Assert.False(finishedWhileLocked);
        Assert.True(result.Succeeded);
        Assert.Equal(WalletStatus.Frozen, (await StoredAsync(wallet)).Status);
    }

    [Fact]
    public async Task Parallel_freezes_of_one_wallet_let_exactly_one_through()
    {
        var (backOffice, _) = await CustomerAsync();
        var (_, wallet) = await CustomerAsync();

        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(i =>
            SetAsync(backOffice.Id, wallet.WalletNumber, WalletStatus.Frozen, $"Reason number {i}")));

        Assert.Equal(1, results.Count(result => result.Succeeded));
        Assert.All(results.Where(result => !result.Succeeded), result => Assert.Equal(ErrorCodes.WalletAlreadyInState, result.ErrorCode));
        await using var check = sql.NewContext();
        Assert.Equal(1, await check.AuditLogs.CountAsync(
            log => log.EntityReference == wallet.WalletNumber && log.Action == AuditActions.WalletFrozen,
            TestContext.Current.CancellationToken));
    }
}
