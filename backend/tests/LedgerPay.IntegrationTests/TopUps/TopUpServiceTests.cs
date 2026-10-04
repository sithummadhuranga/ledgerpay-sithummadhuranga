using LedgerPay.Application.Common;
using LedgerPay.Application.TopUps;
using LedgerPay.Domain.Constants;
using LedgerPay.Domain.Entities;
using LedgerPay.Domain.Enums;
using LedgerPay.IntegrationTests.Support;
using LedgerPay.IntegrationTests.Support.Faults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace LedgerPay.IntegrationTests.TopUps;

public class TopUpServiceTests(SqlServerFixture sql)
{
    private static readonly RequestInfo Caller = new("203.0.113.30", "corr-topup-test");

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

    private async Task<ServiceResult<TopUpResponse>> TopUpAsync(
        Guid operatorId, string? key, TopUpRequest request, TimeProvider? clock = null)
    {
        await using var db = sql.NewContext();
        return await TestServices.TopUps(db, clock).TopUpAsync(operatorId, key, request, Caller, CancellationToken.None);
    }

    private async Task<Wallet> StoredAsync(Wallet wallet)
    {
        await using var check = sql.NewContext();
        return await check.Wallets.AsNoTracking().SingleAsync(w => w.Id == wallet.Id, TestContext.Current.CancellationToken);
    }

    private async Task AssertBalanceMatchesLedgerAsync(Wallet wallet)
    {
        await using var check = sql.NewContext();
        Assert.Equal(await TestData.LedgerBalanceAsync(check, wallet), (await StoredAsync(wallet)).Balance);
    }

    private async Task FreezeAsync(Wallet wallet, WalletStatus status = WalletStatus.Frozen)
    {
        await using var db = sql.NewContext();
        var tracked = await db.Wallets.SingleAsync(w => w.Id == wallet.Id, TestContext.Current.CancellationToken);
        tracked.Status = status;
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Top_up_credits_the_wallet_and_answers_with_the_new_balance()
    {
        var (operatorUser, _) = await CustomerAsync();
        var (_, wallet) = await CustomerAsync(1000.00m);
        var reference = TestServices.NewBankReference();
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 4, 8, 15, 0, TimeSpan.Zero));

        var result = await TopUpAsync(
            operatorUser.Id, TestServices.NewKey(), new TopUpRequest(wallet.WalletNumber, 2500.00m, reference, "Branch deposit"), clock);

        Assert.True(result.Succeeded);
        Assert.Equal(2500.00m, result.Value!.Amount);
        Assert.Equal(3500.00m, result.Value.BalanceAfter);
        Assert.Equal(wallet.WalletNumber, result.Value.WalletNumber);
        Assert.Equal(reference, result.Value.BankReference);
        Assert.Equal(new DateTime(2026, 10, 4, 8, 15, 0, DateTimeKind.Utc), result.Value.CreatedAt);
        Assert.Equal(3500.00m, (await StoredAsync(wallet)).Balance);
    }

    [Fact]
    public async Task Top_up_posts_the_settlement_float_against_the_wallet_with_no_fee()
    {
        var (operatorUser, _) = await CustomerAsync();
        var (_, wallet) = await CustomerAsync();

        var result = await TopUpAsync(
            operatorUser.Id, TestServices.NewKey(), new TopUpRequest(wallet.WalletNumber, 2500.00m, TestServices.NewBankReference(), null));

        await using var check = sql.NewContext();
        var transaction = await check.Transactions.AsNoTracking()
            .SingleAsync(t => t.Reference == result.Value!.Reference, TestContext.Current.CancellationToken);
        var entries = await check.LedgerEntries.AsNoTracking()
            .Where(e => e.TransactionId == transaction.Id).Include(e => e.LedgerAccount).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0m, transaction.Fee);
        Assert.Equal(2, entries.Count);
        Assert.Equal(entries.Sum(e => e.Debit), entries.Sum(e => e.Credit));
        Assert.Equal(2500.00m, entries.Single(e => e.LedgerAccount.Code == LedgerAccountCodes.SettlementFloat).Debit);
        Assert.Equal(2500.00m, entries.Single(e => e.LedgerAccount.WalletId == wallet.Id).Credit);
        await AssertBalanceMatchesLedgerAsync(wallet);
    }

    [Fact]
    public async Task Top_up_is_stored_as_a_completed_top_up_by_the_operator_with_an_audit_entry()
    {
        var (operatorUser, _) = await CustomerAsync();
        var (_, wallet) = await CustomerAsync();
        var reference = TestServices.NewBankReference();

        var result = await TopUpAsync(
            operatorUser.Id, TestServices.NewKey(), new TopUpRequest(wallet.WalletNumber, 750.00m, reference, "Cash at counter"));

        await using var check = sql.NewContext();
        var transaction = await check.Transactions.AsNoTracking()
            .SingleAsync(t => t.Reference == result.Value!.Reference, TestContext.Current.CancellationToken);
        Assert.Equal(TransactionType.TopUp, transaction.Type);
        Assert.Equal(TransactionStatus.Completed, transaction.Status);
        Assert.Null(transaction.SenderWalletId);
        Assert.Equal(wallet.Id, transaction.ReceiverWalletId);
        Assert.Equal(operatorUser.Id, transaction.InitiatedByUserId);
        Assert.Equal(reference, transaction.BankReference);
        Assert.Equal("Cash at counter", transaction.Note);
        var audit = await check.AuditLogs.AsNoTracking()
            .SingleAsync(log => log.EntityReference == transaction.Reference, TestContext.Current.CancellationToken);
        Assert.Equal(AuditActions.TopUp, audit.Action);
        Assert.Equal(operatorUser.Id, audit.ActorUserId);
        Assert.Equal("203.0.113.30", audit.IpAddress);
        Assert.Equal("corr-topup-test", audit.CorrelationId);
    }

    [Fact]
    public async Task The_bank_reference_is_stored_in_upper_case()
    {
        var (operatorUser, _) = await CustomerAsync();
        var (_, wallet) = await CustomerAsync();
        var lower = TestServices.NewBankReference().ToLowerInvariant();

        var result = await TopUpAsync(
            operatorUser.Id, TestServices.NewKey(), new TopUpRequest(wallet.WalletNumber, 500.00m, lower, null));

        Assert.Equal(lower.ToUpperInvariant(), result.Value!.BankReference);
        await using var check = sql.NewContext();
        Assert.Equal(1, await check.Transactions.CountAsync(
            t => t.BankReference == lower.ToUpperInvariant(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_bank_reference_that_a_completed_top_up_used_is_rejected_whatever_its_letter_case()
    {
        var (operatorUser, _) = await CustomerAsync();
        var (_, firstWallet) = await CustomerAsync();
        var (_, secondWallet) = await CustomerAsync();
        var reference = TestServices.NewBankReference();
        await TopUpAsync(operatorUser.Id, TestServices.NewKey(), new TopUpRequest(firstWallet.WalletNumber, 500.00m, reference, null));

        var duplicate = await TopUpAsync(
            operatorUser.Id, TestServices.NewKey(), new TopUpRequest(secondWallet.WalletNumber, 500.00m, reference.ToLowerInvariant(), null));

        Assert.Equal(ErrorCodes.DuplicateBankReference, duplicate.ErrorCode);
        Assert.Equal(0m, (await StoredAsync(secondWallet)).Balance);
        await using var check = sql.NewContext();
        var failed = await check.Transactions.AsNoTracking().SingleAsync(
            t => t.ReceiverWalletId == secondWallet.Id && t.Status == TransactionStatus.Failed, TestContext.Current.CancellationToken);
        Assert.Equal(ErrorCodes.DuplicateBankReference, failed.FailureCode);
        Assert.Equal(0, await check.LedgerEntries.CountAsync(e => e.TransactionId == failed.Id, TestContext.Current.CancellationToken));
        await AssertBalanceMatchesLedgerAsync(secondWallet);
    }

    [Fact]
    public async Task A_top_up_to_a_frozen_wallet_is_rejected_and_the_same_reference_works_after_the_wallet_is_unfrozen()
    {
        var (operatorUser, _) = await CustomerAsync();
        var (_, wallet) = await CustomerAsync();
        await FreezeAsync(wallet);
        var reference = TestServices.NewBankReference();
        var request = new TopUpRequest(wallet.WalletNumber, 500.00m, reference, null);

        var rejected = await TopUpAsync(operatorUser.Id, TestServices.NewKey(), request);
        await FreezeAsync(wallet, WalletStatus.Active);
        var retried = await TopUpAsync(operatorUser.Id, TestServices.NewKey(), request);

        Assert.Equal(ErrorCodes.WalletFrozen, rejected.ErrorCode);
        Assert.True(retried.Succeeded);
        Assert.Equal(500.00m, (await StoredAsync(wallet)).Balance);
        await using var check = sql.NewContext();
        Assert.Equal(1, await check.Transactions.CountAsync(
            t => t.BankReference == reference && t.Status == TransactionStatus.Failed, TestContext.Current.CancellationToken));
        Assert.Equal(1, await check.Transactions.CountAsync(
            t => t.BankReference == reference && t.Status == TransactionStatus.Completed, TestContext.Current.CancellationToken));
        await AssertBalanceMatchesLedgerAsync(wallet);
    }

    [Fact]
    public async Task A_top_up_to_an_unknown_wallet_is_recorded_with_the_number_that_was_typed()
    {
        var (operatorUser, _) = await CustomerAsync();
        var reference = TestServices.NewBankReference();

        var result = await TopUpAsync(
            operatorUser.Id, TestServices.NewKey(), new TopUpRequest("999999999998", 500.00m, reference, null));

        Assert.Equal(ErrorCodes.WalletNotFound, result.ErrorCode);
        await using var check = sql.NewContext();
        var failed = await check.Transactions.AsNoTracking().SingleAsync(
            t => t.BankReference == reference, TestContext.Current.CancellationToken);
        Assert.Equal(TransactionStatus.Failed, failed.Status);
        Assert.Null(failed.ReceiverWalletId);
        Assert.Equal("999999999998", failed.RequestedReceiver);
        var audit = await check.AuditLogs.AsNoTracking()
            .SingleAsync(log => log.EntityReference == failed.Reference, TestContext.Current.CancellationToken);
        Assert.Equal(AuditActions.TopUpFailed, audit.Action);
    }

    [Fact]
    public async Task A_top_up_cannot_push_a_wallet_over_the_balance_cap_but_can_reach_it_exactly()
    {
        var (operatorUser, _) = await CustomerAsync();
        var (_, wallet) = await CustomerAsync(1_999_000.00m);

        var over = await TopUpAsync(
            operatorUser.Id, TestServices.NewKey(), new TopUpRequest(wallet.WalletNumber, 1_000.01m, TestServices.NewBankReference(), null));
        var exact = await TopUpAsync(
            operatorUser.Id, TestServices.NewKey(), new TopUpRequest(wallet.WalletNumber, 1_000.00m, TestServices.NewBankReference(), null));

        Assert.Equal(ErrorCodes.BalanceLimitExceeded, over.ErrorCode);
        Assert.True(exact.Succeeded);
        Assert.Equal(2_000_000.00m, (await StoredAsync(wallet)).Balance);
        await AssertBalanceMatchesLedgerAsync(wallet);
    }

    [Fact]
    public async Task The_same_key_and_payload_tops_up_once()
    {
        var (operatorUser, _) = await CustomerAsync();
        var (_, wallet) = await CustomerAsync();
        var key = TestServices.NewKey();
        var request = new TopUpRequest(wallet.WalletNumber, 800.00m, TestServices.NewBankReference(), null);

        var first = await TopUpAsync(operatorUser.Id, key, request);
        var second = await TopUpAsync(operatorUser.Id, key, request);

        Assert.True(first.Succeeded);
        Assert.False(first.Replayed);
        Assert.True(second.Replayed);
        Assert.Equal(first.Value, second.Value);
        Assert.Equal(800.00m, (await StoredAsync(wallet)).Balance);
        await AssertBalanceMatchesLedgerAsync(wallet);
    }

    [Fact]
    public async Task The_same_key_with_a_different_amount_is_rejected()
    {
        var (operatorUser, _) = await CustomerAsync();
        var (_, wallet) = await CustomerAsync();
        var key = TestServices.NewKey();
        var reference = TestServices.NewBankReference();
        await TopUpAsync(operatorUser.Id, key, new TopUpRequest(wallet.WalletNumber, 800.00m, reference, null));

        var second = await TopUpAsync(operatorUser.Id, key, new TopUpRequest(wallet.WalletNumber, 900.00m, reference, null));

        Assert.Equal(ErrorCodes.IdempotencyKeyReused, second.ErrorCode);
        Assert.Equal(800.00m, (await StoredAsync(wallet)).Balance);
    }

    [Fact]
    public async Task The_same_key_on_the_transfer_endpoint_and_the_top_up_endpoint_is_two_independent_requests()
    {
        var (user, senderWallet) = await CustomerAsync(5000.00m);
        var (_, receiverWallet) = await CustomerAsync();
        var key = TestServices.NewKey();
        await using (var db = sql.NewContext())
        {
            var transfer = await TestServices.Transfers(db).TransferAsync(
                user.Id, key, new LedgerPay.Application.Transfers.TransferRequest(receiverWallet.WalletNumber, null, 1000.00m, null),
                Caller, TestContext.Current.CancellationToken);
            Assert.True(transfer.Succeeded);
        }

        var topUp = await TopUpAsync(
            user.Id, key, new TopUpRequest(senderWallet.WalletNumber, 300.00m, TestServices.NewBankReference(), null));

        Assert.True(topUp.Succeeded);
        Assert.False(topUp.Replayed);
        Assert.Equal(5000.00m - 1010.00m + 300.00m, (await StoredAsync(senderWallet)).Balance);
    }

    [Fact]
    public async Task Two_parallel_top_ups_with_one_bank_reference_let_only_one_through()
    {
        var (operatorUser, _) = await CustomerAsync();
        var (_, walletOne) = await CustomerAsync();
        var (_, walletTwo) = await CustomerAsync();
        var reference = TestServices.NewBankReference();

        var results = await Task.WhenAll(
            TopUpAsync(operatorUser.Id, TestServices.NewKey(), new TopUpRequest(walletOne.WalletNumber, 500.00m, reference, null)),
            TopUpAsync(operatorUser.Id, TestServices.NewKey(), new TopUpRequest(walletTwo.WalletNumber, 500.00m, reference, null)));

        Assert.Equal(1, results.Count(result => result.Succeeded));
        Assert.Equal(ErrorCodes.DuplicateBankReference, results.Single(result => !result.Succeeded).ErrorCode);
        Assert.Equal(500.00m, (await StoredAsync(walletOne)).Balance + (await StoredAsync(walletTwo)).Balance);
        await AssertBalanceMatchesLedgerAsync(walletOne);
        await AssertBalanceMatchesLedgerAsync(walletTwo);
    }

    [Fact]
    public async Task Parallel_top_ups_to_one_wallet_all_land_and_the_balance_matches_the_ledger()
    {
        var (operatorUser, _) = await CustomerAsync();
        var (_, wallet) = await CustomerAsync();

        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => TopUpAsync(
            operatorUser.Id, TestServices.NewKey(), new TopUpRequest(wallet.WalletNumber, 100.00m, TestServices.NewBankReference(), null))));

        Assert.All(results, result => Assert.True(result.Succeeded));
        Assert.Equal(1000.00m, (await StoredAsync(wallet)).Balance);
        await AssertBalanceMatchesLedgerAsync(wallet);
    }

    [Fact]
    public async Task A_rejected_top_up_is_replayed_under_its_key_even_after_the_cause_is_gone()
    {
        var (operatorUser, _) = await CustomerAsync();
        var (_, wallet) = await CustomerAsync();
        await FreezeAsync(wallet);
        var key = TestServices.NewKey();
        var request = new TopUpRequest(wallet.WalletNumber, 500.00m, TestServices.NewBankReference(), null);
        var first = await TopUpAsync(operatorUser.Id, key, request);
        await FreezeAsync(wallet, WalletStatus.Active);

        var again = await TopUpAsync(operatorUser.Id, key, request);
        var withNewKey = await TopUpAsync(operatorUser.Id, TestServices.NewKey(), request);

        Assert.Equal(ErrorCodes.WalletFrozen, first.ErrorCode);
        Assert.Equal(ErrorCodes.WalletFrozen, again.ErrorCode);
        Assert.True(again.Replayed);
        Assert.True(withNewKey.Succeeded);
        Assert.Equal(500.00m, (await StoredAsync(wallet)).Balance);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task A_missing_key_is_refused_before_anything_is_written(string? key)
    {
        var (operatorUser, _) = await CustomerAsync();
        var (_, wallet) = await CustomerAsync();
        var reference = TestServices.NewBankReference();

        var result = await TopUpAsync(operatorUser.Id, key, new TopUpRequest(wallet.WalletNumber, 500.00m, reference, null));

        Assert.Equal(ErrorCodes.IdempotencyKeyRequired, result.ErrorCode);
        await using var check = sql.NewContext();
        Assert.Equal(0, await check.Transactions.CountAsync(t => t.BankReference == reference, TestContext.Current.CancellationToken));
        Assert.Equal(0m, (await StoredAsync(wallet)).Balance);
    }

    [Fact]
    public async Task Eight_parallel_top_ups_with_one_key_land_once()
    {
        var (operatorUser, _) = await CustomerAsync();
        var (_, wallet) = await CustomerAsync();
        var key = TestServices.NewKey();
        var request = new TopUpRequest(wallet.WalletNumber, 400.00m, TestServices.NewBankReference(), null);

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => TopUpAsync(operatorUser.Id, key, request)));

        Assert.All(results, result => Assert.True(result.Succeeded));
        Assert.Single(results.Select(result => result.Value!.Reference).Distinct());
        Assert.Equal(1, results.Count(result => !result.Replayed));
        Assert.Equal(400.00m, (await StoredAsync(wallet)).Balance);
        await AssertBalanceMatchesLedgerAsync(wallet);
    }

    [Fact]
    public async Task A_commit_that_succeeds_but_reports_an_error_is_retried_into_a_replay_and_tops_up_once()
    {
        var (operatorUser, _) = await CustomerAsync();
        var (_, wallet) = await CustomerAsync();
        await using var db = FaultyContext.Create(sql.AdminConnectionString, new FailAfterCommitOnce());

        var result = await TestServices.TopUps(db).TopUpAsync(
            operatorUser.Id,
            TestServices.NewKey(),
            new TopUpRequest(wallet.WalletNumber, 900.00m, TestServices.NewBankReference(), null),
            Caller,
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.True(result.Replayed);
        Assert.Equal(900.00m, (await StoredAsync(wallet)).Balance);
        await AssertBalanceMatchesLedgerAsync(wallet);
    }
}
