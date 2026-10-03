using LedgerPay.Application.Common;
using LedgerPay.Application.Settings;
using LedgerPay.Application.Transfers;
using LedgerPay.Domain.Constants;
using LedgerPay.Domain.Entities;
using LedgerPay.Domain.Enums;
using LedgerPay.Infrastructure.Persistence;
using LedgerPay.IntegrationTests.Support;
using LedgerPay.IntegrationTests.Support.Faults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace LedgerPay.IntegrationTests.Transfers;

public class TransferServiceTests(SqlServerFixture sql)
{
    private static readonly RequestInfo Caller = new("203.0.113.7", "corr-transfer-test");

    private static TransferService NewService(AppDbContext db, TimeProvider? clock = null) =>
        new(db, new LedgerSettingsProvider(db), clock ?? TimeProvider.System);

    private static TransferRequest To(Wallet receiver, decimal amount, string? note = null) =>
        new(receiver.WalletNumber, null, amount, note);

    private async Task<(User User, Wallet Wallet)> FundedCustomerAsync(decimal balance)
    {
        await using var db = sql.NewContext();
        var (user, wallet, _) = await TestData.AddCustomerAsync(db);
        if (balance > 0)
        {
            await TestData.FundAsync(db, wallet, balance);
        }

        return (user, wallet);
    }

    private async Task AssertBalancesMatchLedgerAsync(params Wallet[] wallets)
    {
        await using var check = sql.NewContext();
        foreach (var wallet in wallets)
        {
            var stored = await check.Wallets.AsNoTracking().SingleAsync(w => w.Id == wallet.Id, TestContext.Current.CancellationToken);
            Assert.Equal(await TestData.LedgerBalanceAsync(check, wallet), stored.Balance);
        }
    }

    private async Task<Transaction> OnlyFailedTransferOfAsync(Wallet sender)
    {
        await using var check = sql.NewContext();
        return await check.Transactions.AsNoTracking().SingleAsync(
            t => t.SenderWalletId == sender.Id && t.Status == TransactionStatus.Failed, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Transfer_moves_the_amount_to_the_receiver_and_charges_the_fee_to_the_sender()
    {
        var (sender, senderWallet) = await FundedCustomerAsync(5000.00m);
        var (_, receiverWallet) = await FundedCustomerAsync(0m);
        await using var db = sql.NewContext();

        var result = await NewService(db).TransferAsync(
            sender.Id, To(receiverWallet, 1000.00m, "Rent for October"), Caller, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(1000.00m, result.Value!.Amount);
        Assert.Equal(10.00m, result.Value.Fee);
        Assert.Equal(1010.00m, result.Value.Total);
        Assert.Equal(3990.00m, result.Value.BalanceAfter);
        await using var check = sql.NewContext();
        Assert.Equal(3990.00m, (await check.Wallets.SingleAsync(w => w.Id == senderWallet.Id, TestContext.Current.CancellationToken)).Balance);
        Assert.Equal(1000.00m, (await check.Wallets.SingleAsync(w => w.Id == receiverWallet.Id, TestContext.Current.CancellationToken)).Balance);
    }

    [Fact]
    public async Task Transfer_can_address_the_recipient_by_phone()
    {
        var (sender, _) = await FundedCustomerAsync(5000.00m);
        await using var setup = sql.NewContext();
        var (receiverUser, receiverWallet, _) = await TestData.AddCustomerAsync(setup);
        await using var db = sql.NewContext();

        var result = await NewService(db).TransferAsync(
            sender.Id, new TransferRequest(null, receiverUser.Phone, 500.00m, null), Caller, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(receiverWallet.WalletNumber, result.Value!.RecipientWalletNumber);
    }

    [Fact]
    public async Task Transfer_posts_balanced_entries()
    {
        var (sender, _) = await FundedCustomerAsync(5000.00m);
        var (_, receiverWallet) = await FundedCustomerAsync(0m);
        await using var db = sql.NewContext();

        var result = await NewService(db).TransferAsync(
            sender.Id, To(receiverWallet, 1000.00m), Caller, TestContext.Current.CancellationToken);

        var entries = await db.LedgerEntries.AsNoTracking()
            .Where(entry => entry.Transaction.Reference == result.Value!.Reference)
            .Include(entry => entry.LedgerAccount)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(3, entries.Count);
        Assert.Equal(entries.Sum(entry => entry.Debit), entries.Sum(entry => entry.Credit));
        Assert.Equal(1010.00m, entries.Single(entry => entry.Debit > 0).Debit);
        Assert.Equal(10.00m, entries.Single(entry => entry.LedgerAccount.Code == LedgerAccountCodes.FeeRevenue).Credit);
    }

    [Fact]
    public async Task Wallet_balances_still_match_the_ledger_after_a_transfer()
    {
        var (sender, senderWallet) = await FundedCustomerAsync(5000.00m);
        var (_, receiverWallet) = await FundedCustomerAsync(250.00m);
        await using var db = sql.NewContext();

        await NewService(db).TransferAsync(sender.Id, To(receiverWallet, 1234.56m), Caller, TestContext.Current.CancellationToken);

        await using var check = sql.NewContext();
        foreach (var wallet in new[] { senderWallet, receiverWallet })
        {
            var stored = await check.Wallets.AsNoTracking().SingleAsync(w => w.Id == wallet.Id, TestContext.Current.CancellationToken);
            Assert.Equal(await TestData.LedgerBalanceAsync(check, wallet), stored.Balance);
        }
    }

    [Fact]
    public async Task Completed_transfer_is_stored_with_note_time_and_audit_entry()
    {
        var (sender, senderWallet) = await FundedCustomerAsync(5000.00m);
        var (_, receiverWallet) = await FundedCustomerAsync(0m);
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 3, 9, 30, 0, TimeSpan.Zero));
        await using var db = sql.NewContext();

        var result = await NewService(db, clock).TransferAsync(
            sender.Id, To(receiverWallet, 1000.00m, "Rent for October"), Caller, TestContext.Current.CancellationToken);

        await using var check = sql.NewContext();
        var transaction = await check.Transactions.AsNoTracking()
            .SingleAsync(t => t.Reference == result.Value!.Reference, TestContext.Current.CancellationToken);
        Assert.Equal(TransactionType.Transfer, transaction.Type);
        Assert.Equal(TransactionStatus.Completed, transaction.Status);
        Assert.Equal("Rent for October", transaction.Note);
        Assert.Equal(new DateTime(2026, 10, 3, 9, 30, 0, DateTimeKind.Utc), transaction.CreatedAt);
        Assert.Equal(senderWallet.Id, transaction.SenderWalletId);
        Assert.Equal(receiverWallet.Id, transaction.ReceiverWalletId);
        var audit = await check.AuditLogs.AsNoTracking()
            .SingleAsync(log => log.EntityReference == transaction.Reference, TestContext.Current.CancellationToken);
        Assert.Equal(AuditActions.Transfer, audit.Action);
        Assert.Equal(sender.Id, audit.ActorUserId);
        Assert.Equal("203.0.113.7", audit.IpAddress);
        Assert.Equal("corr-transfer-test", audit.CorrelationId);
    }

    [Fact]
    public async Task Failed_transfer_leaves_one_failed_row_and_no_ledger_entries()
    {
        var (sender, senderWallet) = await FundedCustomerAsync(1000.00m);
        var (_, receiverWallet) = await FundedCustomerAsync(0m);
        await using var db = sql.NewContext();

        var result = await NewService(db).TransferAsync(
            sender.Id, To(receiverWallet, 1000.00m), Caller, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.InsufficientFunds, result.ErrorCode);
        await using var check = sql.NewContext();
        var failed = await check.Transactions.AsNoTracking()
            .SingleAsync(t => t.SenderWalletId == senderWallet.Id && t.Status == TransactionStatus.Failed, TestContext.Current.CancellationToken);
        Assert.Equal(ErrorCodes.InsufficientFunds, failed.FailureCode);
        Assert.Equal(1000.00m, failed.Amount);
        Assert.Equal(0, await check.LedgerEntries.CountAsync(e => e.TransactionId == failed.Id, TestContext.Current.CancellationToken));
        Assert.Equal(1000.00m, (await check.Wallets.AsNoTracking().SingleAsync(w => w.Id == senderWallet.Id, TestContext.Current.CancellationToken)).Balance);
        Assert.Equal(0m, (await check.Wallets.AsNoTracking().SingleAsync(w => w.Id == receiverWallet.Id, TestContext.Current.CancellationToken)).Balance);
        var audit = await check.AuditLogs.AsNoTracking()
            .SingleAsync(log => log.EntityReference == failed.Reference, TestContext.Current.CancellationToken);
        Assert.Equal(AuditActions.TransferFailed, audit.Action);
        await AssertBalancesMatchLedgerAsync(senderWallet, receiverWallet);
    }

    [Fact]
    public async Task Transfer_to_an_unknown_wallet_number_is_recorded_with_what_the_sender_typed()
    {
        var (sender, senderWallet) = await FundedCustomerAsync(5000.00m);
        await using var db = sql.NewContext();

        var result = await NewService(db).TransferAsync(
            sender.Id, new TransferRequest("999999999999", null, 500.00m, null), Caller, TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.RecipientNotFound, result.ErrorCode);
        await using var check = sql.NewContext();
        var failed = await check.Transactions.AsNoTracking()
            .SingleAsync(t => t.SenderWalletId == senderWallet.Id && t.Status == TransactionStatus.Failed, TestContext.Current.CancellationToken);
        Assert.Null(failed.ReceiverWalletId);
        Assert.Equal("999999999999", failed.RequestedReceiver);
    }

    [Fact]
    public async Task Transfer_to_yourself_is_rejected_and_moves_nothing()
    {
        var (sender, senderWallet) = await FundedCustomerAsync(5000.00m);
        await using var db = sql.NewContext();

        var result = await NewService(db).TransferAsync(
            sender.Id, To(senderWallet, 500.00m), Caller, TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.SelfTransferNotAllowed, result.ErrorCode);
        await using var check = sql.NewContext();
        Assert.Equal(5000.00m, (await check.Wallets.AsNoTracking().SingleAsync(w => w.Id == senderWallet.Id, TestContext.Current.CancellationToken)).Balance);
        await AssertBalancesMatchLedgerAsync(senderWallet);
    }

    private async Task FreezeAsync(Wallet wallet)
    {
        await using var freeze = sql.NewContext();
        var tracked = await freeze.Wallets.SingleAsync(w => w.Id == wallet.Id, TestContext.Current.CancellationToken);
        tracked.Status = WalletStatus.Frozen;
        await freeze.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Frozen_recipient_cannot_receive_and_nothing_moves()
    {
        var (sender, senderWallet) = await FundedCustomerAsync(5000.00m);
        var (_, receiverWallet) = await FundedCustomerAsync(0m);
        await FreezeAsync(receiverWallet);
        await using var db = sql.NewContext();

        var result = await NewService(db).TransferAsync(
            sender.Id, To(receiverWallet, 500.00m), Caller, TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.WalletFrozen, result.ErrorCode);
        Assert.Equal(ErrorCodes.WalletFrozen, (await OnlyFailedTransferOfAsync(senderWallet)).FailureCode);
        await using var check = sql.NewContext();
        Assert.Equal(5000.00m, (await check.Wallets.AsNoTracking().SingleAsync(w => w.Id == senderWallet.Id, TestContext.Current.CancellationToken)).Balance);
        await AssertBalancesMatchLedgerAsync(senderWallet, receiverWallet);
    }

    [Fact]
    public async Task Frozen_sender_cannot_send_and_nothing_moves()
    {
        var (sender, senderWallet) = await FundedCustomerAsync(5000.00m);
        var (_, receiverWallet) = await FundedCustomerAsync(0m);
        await FreezeAsync(senderWallet);
        await using var db = sql.NewContext();

        var result = await NewService(db).TransferAsync(
            sender.Id, To(receiverWallet, 500.00m), Caller, TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.WalletFrozen, result.ErrorCode);
        Assert.Equal(ErrorCodes.WalletFrozen, (await OnlyFailedTransferOfAsync(senderWallet)).FailureCode);
        await using var check = sql.NewContext();
        Assert.Equal(5000.00m, (await check.Wallets.AsNoTracking().SingleAsync(w => w.Id == senderWallet.Id, TestContext.Current.CancellationToken)).Balance);
        await AssertBalancesMatchLedgerAsync(senderWallet, receiverWallet);
    }

    [Fact]
    public async Task Amount_below_the_minimum_is_rejected_and_recorded()
    {
        var (sender, senderWallet) = await FundedCustomerAsync(5000.00m);
        var (_, receiverWallet) = await FundedCustomerAsync(0m);
        await using var db = sql.NewContext();

        var result = await NewService(db).TransferAsync(
            sender.Id, To(receiverWallet, 99.99m), Caller, TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.AmountBelowMinimum, result.ErrorCode);
        var failed = await OnlyFailedTransferOfAsync(senderWallet);
        Assert.Equal(ErrorCodes.AmountBelowMinimum, failed.FailureCode);
        Assert.Equal(99.99m, failed.Amount);
        await AssertBalancesMatchLedgerAsync(senderWallet, receiverWallet);
    }

    [Fact]
    public async Task Amount_above_the_maximum_is_rejected_and_recorded()
    {
        var (sender, senderWallet) = await FundedCustomerAsync(5000.00m);
        var (_, receiverWallet) = await FundedCustomerAsync(0m);
        await using var db = sql.NewContext();

        var result = await NewService(db).TransferAsync(
            sender.Id, To(receiverWallet, 500_000.01m), Caller, TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.AmountAboveMaximum, result.ErrorCode);
        Assert.Equal(ErrorCodes.AmountAboveMaximum, (await OnlyFailedTransferOfAsync(senderWallet)).FailureCode);
        await AssertBalancesMatchLedgerAsync(senderWallet, receiverWallet);
    }

    [Fact]
    public async Task A_transient_fault_after_saving_makes_the_transfer_run_again_and_it_still_posts_once()
    {
        var (sender, senderWallet) = await FundedCustomerAsync(5000.00m);
        var (_, receiverWallet) = await FundedCustomerAsync(0m);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(sql.AdminConnectionString, server => server.ExecutionStrategy(dependencies => new TransientRetryStrategy(dependencies)))
            .AddInterceptors(new FailOnceAfterSave())
            .Options;
        await using var db = new AppDbContext(options);

        var result = await NewService(db).TransferAsync(
            sender.Id, To(receiverWallet, 1000.00m), Caller, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        await using var check = sql.NewContext();
        Assert.Equal(1, await check.Transactions.CountAsync(
            t => t.SenderWalletId == senderWallet.Id && t.Status == TransactionStatus.Completed, TestContext.Current.CancellationToken));
        Assert.Equal(3, await check.LedgerEntries.CountAsync(
            e => e.Transaction.SenderWalletId == senderWallet.Id && e.Transaction.Type == TransactionType.Transfer, TestContext.Current.CancellationToken));
        Assert.Equal(1, await check.AuditLogs.CountAsync(
            log => log.ActorUserId == sender.Id && log.Action == AuditActions.Transfer, TestContext.Current.CancellationToken));
        Assert.Equal(3990.00m, (await check.Wallets.AsNoTracking().SingleAsync(w => w.Id == senderWallet.Id, TestContext.Current.CancellationToken)).Balance);
        Assert.Equal(1000.00m, (await check.Wallets.AsNoTracking().SingleAsync(w => w.Id == receiverWallet.Id, TestContext.Current.CancellationToken)).Balance);
        await AssertBalancesMatchLedgerAsync(senderWallet, receiverWallet);
    }

    [Fact]
    public async Task Recipient_cannot_be_pushed_over_the_balance_cap()
    {
        var (sender, senderWallet) = await FundedCustomerAsync(5000.00m);
        var (_, receiverWallet) = await FundedCustomerAsync(1_999_900.00m);
        await using var db = sql.NewContext();

        var result = await NewService(db).TransferAsync(
            sender.Id, To(receiverWallet, 200.00m), Caller, TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.ReceiverBalanceLimitExceeded, result.ErrorCode);
        await AssertBalancesMatchLedgerAsync(senderWallet, receiverWallet);
    }

    [Fact]
    public async Task Quote_returns_the_fee_and_total_for_an_amount()
    {
        await using var db = sql.NewContext();

        var result = await NewService(db).QuoteAsync(5000.00m, TestContext.Current.CancellationToken);

        Assert.Equal((5000.00m, 25.00m, 5025.00m), (result.Value!.Amount, result.Value.Fee, result.Value.Total));
    }

    [Fact]
    public async Task Quote_refuses_an_amount_outside_the_transfer_limits()
    {
        await using var db = sql.NewContext();
        var service = NewService(db);

        var below = await service.QuoteAsync(99.99m, TestContext.Current.CancellationToken);
        var above = await service.QuoteAsync(500_000.01m, TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.AmountBelowMinimum, below.ErrorCode);
        Assert.Equal(ErrorCodes.AmountAboveMaximum, above.ErrorCode);
    }

    [Fact]
    public async Task A_user_without_a_wallet_gets_wallet_not_found_instead_of_an_error()
    {
        var (_, receiverWallet) = await FundedCustomerAsync(0m);
        await using var db = sql.NewContext();
        var operatorUser = new User
        {
            Email = $"operator.{Guid.NewGuid():N}@example.com",
            Phone = "+947" + Random.Shared.Next(10_000_000, 99_999_999),
            FullName = "Dilani Senanayake",
            PasswordHash = "hash-not-used-in-these-tests",
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(operatorUser);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await NewService(db).TransferAsync(
            operatorUser.Id, To(receiverWallet, 500.00m), Caller, TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.WalletNotFound, result.ErrorCode);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task A_request_without_exactly_one_recipient_is_refused_before_anything_is_written(bool withNumber, bool withPhone)
    {
        var (sender, senderWallet) = await FundedCustomerAsync(5000.00m);
        var (receiverUser, receiverWallet) = await FundedCustomerAsync(0m);
        await using var db = sql.NewContext();
        var request = new TransferRequest(
            withNumber ? receiverWallet.WalletNumber : null, withPhone ? "+94771234567" : null, 500.00m, null);

        var result = await NewService(db).TransferAsync(sender.Id, request, Caller, TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        await using var check = sql.NewContext();
        Assert.Equal(0, await check.Transactions.CountAsync(
            t => t.SenderWalletId == senderWallet.Id && t.Type == TransactionType.Transfer, TestContext.Current.CancellationToken));
    }
}
