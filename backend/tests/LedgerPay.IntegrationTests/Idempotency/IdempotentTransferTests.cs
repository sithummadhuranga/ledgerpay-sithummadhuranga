using LedgerPay.Application.Common;
using LedgerPay.Application.Idempotency;
using LedgerPay.Application.Transfers;
using LedgerPay.Domain.Constants;
using LedgerPay.Domain.Entities;
using LedgerPay.Domain.Enums;
using LedgerPay.IntegrationTests.Support;
using LedgerPay.IntegrationTests.Support.Faults;
using Microsoft.EntityFrameworkCore;

namespace LedgerPay.IntegrationTests.Idempotency;

public class IdempotentTransferTests(SqlServerFixture sql)
{
    private static readonly RequestInfo Caller = new("203.0.113.20", "corr-idempotency-test");

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

    private static TransferRequest To(Wallet receiver, decimal amount) => new(receiver.WalletNumber, null, amount, null);

    // Every call gets a context of its own, the way every HTTP request gets one.
    private async Task<ServiceResult<TransferResponse>> SendAsync(Guid userId, string? key, TransferRequest request)
    {
        await using var db = sql.NewContext();
        return await TestServices.Transfers(db).TransferAsync(userId, key, request, Caller, CancellationToken.None);
    }

    private async Task<decimal> BalanceAsync(Wallet wallet)
    {
        await using var check = sql.NewContext();
        return (await check.Wallets.AsNoTracking().SingleAsync(w => w.Id == wallet.Id, TestContext.Current.CancellationToken)).Balance;
    }

    private async Task<int> CountTransfersAsync(Wallet sender, TransactionStatus status)
    {
        await using var check = sql.NewContext();
        return await check.Transactions.CountAsync(
            t => t.SenderWalletId == sender.Id && t.Type == TransactionType.Transfer && t.Status == status,
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task The_same_key_and_payload_moves_money_once_and_returns_the_original_result()
    {
        var (sender, senderWallet) = await CustomerAsync(5000.00m);
        var (_, receiverWallet) = await CustomerAsync(0m);
        var key = TestServices.NewKey();

        var first = await SendAsync(sender.Id, key, To(receiverWallet, 1000.00m));
        var second = await SendAsync(sender.Id, key, To(receiverWallet, 1000.00m));

        Assert.True(first.Succeeded);
        Assert.False(first.Replayed);
        Assert.True(second.Succeeded);
        Assert.True(second.Replayed);
        Assert.Equal(first.Value, second.Value);
        Assert.Equal(3990.00m, await BalanceAsync(senderWallet));
        Assert.Equal(1000.00m, await BalanceAsync(receiverWallet));
        Assert.Equal(1, await CountTransfersAsync(senderWallet, TransactionStatus.Completed));
        await using var check = sql.NewContext();
        Assert.Equal(3, await check.LedgerEntries.CountAsync(
            e => e.Transaction.SenderWalletId == senderWallet.Id && e.Transaction.Type == TransactionType.Transfer,
            TestContext.Current.CancellationToken));
        Assert.Equal(1, await check.AuditLogs.CountAsync(
            log => log.ActorUserId == sender.Id && log.Action == AuditActions.Transfer, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task The_same_key_with_a_different_amount_is_rejected_and_moves_nothing_more()
    {
        var (sender, senderWallet) = await CustomerAsync(5000.00m);
        var (_, receiverWallet) = await CustomerAsync(0m);
        var key = TestServices.NewKey();
        await SendAsync(sender.Id, key, To(receiverWallet, 1000.00m));

        var second = await SendAsync(sender.Id, key, To(receiverWallet, 1500.00m));

        Assert.False(second.Succeeded);
        Assert.Equal(ErrorCodes.IdempotencyKeyReused, second.ErrorCode);
        Assert.Equal(3990.00m, await BalanceAsync(senderWallet));
        Assert.Equal(1, await CountTransfersAsync(senderWallet, TransactionStatus.Completed));
    }

    [Fact]
    public async Task The_same_key_with_a_different_recipient_is_rejected()
    {
        var (sender, senderWallet) = await CustomerAsync(5000.00m);
        var (_, firstReceiver) = await CustomerAsync(0m);
        var (_, secondReceiver) = await CustomerAsync(0m);
        var key = TestServices.NewKey();
        await SendAsync(sender.Id, key, To(firstReceiver, 1000.00m));

        var second = await SendAsync(sender.Id, key, To(secondReceiver, 1000.00m));

        Assert.Equal(ErrorCodes.IdempotencyKeyReused, second.ErrorCode);
        Assert.Equal(0m, await BalanceAsync(secondReceiver));
        Assert.Equal(1, await CountTransfersAsync(senderWallet, TransactionStatus.Completed));
    }

    [Fact]
    public async Task A_rejected_transfer_is_stored_under_its_key_and_replayed_instead_of_tried_again()
    {
        var (sender, senderWallet) = await CustomerAsync(500.00m);
        var (_, receiverWallet) = await CustomerAsync(0m);
        var key = TestServices.NewKey();
        var first = await SendAsync(sender.Id, key, To(receiverWallet, 1000.00m));
        await using (var topUp = sql.NewContext())
        {
            await TestData.FundAsync(topUp, senderWallet, 5000.00m);
        }

        var second = await SendAsync(sender.Id, key, To(receiverWallet, 1000.00m));

        Assert.Equal(ErrorCodes.InsufficientFunds, first.ErrorCode);
        Assert.False(first.Replayed);
        Assert.Equal(ErrorCodes.InsufficientFunds, second.ErrorCode);
        Assert.True(second.Replayed);
        Assert.Equal(1, await CountTransfersAsync(senderWallet, TransactionStatus.Failed));
        Assert.Equal(0, await CountTransfersAsync(senderWallet, TransactionStatus.Completed));
        Assert.Equal(5500.00m, await BalanceAsync(senderWallet));
    }

    [Fact]
    public async Task A_new_key_makes_a_new_attempt_after_a_rejection()
    {
        var (sender, senderWallet) = await CustomerAsync(500.00m);
        var (_, receiverWallet) = await CustomerAsync(0m);
        await SendAsync(sender.Id, TestServices.NewKey(), To(receiverWallet, 1000.00m));
        await using (var topUp = sql.NewContext())
        {
            await TestData.FundAsync(topUp, senderWallet, 5000.00m);
        }

        var retry = await SendAsync(sender.Id, TestServices.NewKey(), To(receiverWallet, 1000.00m));

        Assert.True(retry.Succeeded);
        Assert.False(retry.Replayed);
        Assert.Equal(4490.00m, await BalanceAsync(senderWallet));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_missing_key_is_refused_and_nothing_is_written(string? key)
    {
        var (sender, senderWallet) = await CustomerAsync(5000.00m);
        var (_, receiverWallet) = await CustomerAsync(0m);

        var result = await SendAsync(sender.Id, key, To(receiverWallet, 1000.00m));

        Assert.Equal(ErrorCodes.IdempotencyKeyRequired, result.ErrorCode);
        Assert.Equal(0, await CountTransfersAsync(senderWallet, TransactionStatus.Completed));
        Assert.Equal(0, await CountTransfersAsync(senderWallet, TransactionStatus.Failed));
        await using var check = sql.NewContext();
        Assert.Equal(0, await check.IdempotencyKeys.CountAsync(k => k.UserId == sender.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_key_longer_than_the_column_is_a_validation_error_and_nothing_is_written()
    {
        var (sender, senderWallet) = await CustomerAsync(5000.00m);
        var (_, receiverWallet) = await CustomerAsync(0m);

        var result = await SendAsync(sender.Id, new string('k', IdempotencyService.MaximumKeyLength + 1), To(receiverWallet, 1000.00m));

        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        Assert.Equal(0, await CountTransfersAsync(senderWallet, TransactionStatus.Completed));
    }

    [Fact]
    public async Task A_key_of_exactly_the_maximum_length_is_accepted()
    {
        var (sender, _) = await CustomerAsync(5000.00m);
        var (_, receiverWallet) = await CustomerAsync(0m);

        var result = await SendAsync(sender.Id, new string('k', IdempotencyService.MaximumKeyLength), To(receiverWallet, 1000.00m));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task The_same_key_from_two_customers_is_two_independent_requests()
    {
        var (senderOne, walletOne) = await CustomerAsync(5000.00m);
        var (senderTwo, walletTwo) = await CustomerAsync(5000.00m);
        var (_, receiverWallet) = await CustomerAsync(0m);
        var key = TestServices.NewKey();

        var first = await SendAsync(senderOne.Id, key, To(receiverWallet, 1000.00m));
        var second = await SendAsync(senderTwo.Id, key, To(receiverWallet, 1000.00m));

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.False(second.Replayed);
        Assert.NotEqual(first.Value!.Reference, second.Value!.Reference);
        Assert.Equal(3990.00m, await BalanceAsync(walletOne));
        Assert.Equal(3990.00m, await BalanceAsync(walletTwo));
    }

    [Fact]
    public async Task The_answer_is_stored_on_the_key_row_together_with_its_status_and_transaction()
    {
        var (sender, _) = await CustomerAsync(5000.00m);
        var (_, receiverWallet) = await CustomerAsync(0m);
        var key = TestServices.NewKey();
        var request = To(receiverWallet, 1000.00m);

        var result = await SendAsync(sender.Id, key, request);

        await using var check = sql.NewContext();
        var row = await check.IdempotencyKeys.AsNoTracking()
            .SingleAsync(k => k.UserId == sender.Id && k.Key == key, TestContext.Current.CancellationToken);
        var transaction = await check.Transactions.AsNoTracking()
            .SingleAsync(t => t.Reference == result.Value!.Reference, TestContext.Current.CancellationToken);
        Assert.Equal(IdempotencyEndpoints.Transfers, row.Endpoint);
        Assert.Equal(RequestHasher.Hash(request), row.RequestHash);
        Assert.Equal(201, row.ResponseStatusCode);
        Assert.Contains(result.Value!.Reference, row.ResponseBody);
        Assert.Equal(transaction.Id, row.TransactionId);
    }

    [Fact]
    public async Task A_rejection_is_stored_with_its_error_status_and_the_failed_transaction()
    {
        var (sender, senderWallet) = await CustomerAsync(100.00m);
        var (_, receiverWallet) = await CustomerAsync(0m);
        var key = TestServices.NewKey();

        await SendAsync(sender.Id, key, To(receiverWallet, 1000.00m));

        await using var check = sql.NewContext();
        var row = await check.IdempotencyKeys.AsNoTracking()
            .SingleAsync(k => k.UserId == sender.Id && k.Key == key, TestContext.Current.CancellationToken);
        var failed = await check.Transactions.AsNoTracking().SingleAsync(
            t => t.SenderWalletId == senderWallet.Id && t.Status == TransactionStatus.Failed, TestContext.Current.CancellationToken);
        Assert.Equal(422, row.ResponseStatusCode);
        Assert.Equal(ErrorCodes.InsufficientFunds, row.ResponseBody);
        Assert.Equal(failed.Id, row.TransactionId);
    }

    [Fact]
    public async Task Ten_parallel_requests_with_one_key_move_money_once()
    {
        var (sender, senderWallet) = await CustomerAsync(5000.00m);
        var (_, receiverWallet) = await CustomerAsync(0m);
        var key = TestServices.NewKey();

        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => SendAsync(sender.Id, key, To(receiverWallet, 1000.00m))));

        Assert.All(results, result => Assert.True(result.Succeeded));
        Assert.Single(results.Select(result => result.Value!.Reference).Distinct());
        Assert.Equal(1, results.Count(result => !result.Replayed));
        Assert.Equal(9, results.Count(result => result.Replayed));
        Assert.Equal(3990.00m, await BalanceAsync(senderWallet));
        Assert.Equal(1000.00m, await BalanceAsync(receiverWallet));
        Assert.Equal(1, await CountTransfersAsync(senderWallet, TransactionStatus.Completed));
    }

    [Fact]
    public async Task Parallel_requests_with_one_key_and_two_different_amounts_let_only_one_amount_through()
    {
        var (sender, senderWallet) = await CustomerAsync(5000.00m);
        var (_, receiverWallet) = await CustomerAsync(0m);
        var key = TestServices.NewKey();

        var results = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(i => SendAsync(sender.Id, key, To(receiverWallet, i % 2 == 0 ? 500.00m : 600.00m))));

        var winners = results.Where(result => result.Succeeded && !result.Replayed).ToList();
        Assert.Single(winners);
        var winningAmount = winners[0].Value!.Amount;
        Assert.All(results, result =>
        {
            if (result.Succeeded)
            {
                Assert.Equal(winners[0].Value!.Reference, result.Value!.Reference);
            }
            else
            {
                Assert.Equal(ErrorCodes.IdempotencyKeyReused, result.ErrorCode);
            }
        });
        Assert.Equal(1, await CountTransfersAsync(senderWallet, TransactionStatus.Completed));
        Assert.Equal(winningAmount, await BalanceAsync(receiverWallet));
    }

    [Fact]
    public async Task A_missing_key_is_reported_before_the_wallet_and_recipient_are_looked_up()
    {
        var userWithoutWallet = await NewUserWithoutWalletAsync();

        var result = await SendAsync(userWithoutWallet.Id, null, new TransferRequest("999999999996", null, 500.00m, null));

        Assert.Equal(ErrorCodes.IdempotencyKeyRequired, result.ErrorCode);
    }

    [Fact]
    public async Task Keys_that_differ_only_in_letter_case_are_the_same_key()
    {
        var (sender, senderWallet) = await CustomerAsync(5000.00m);
        var (_, receiverWallet) = await CustomerAsync(0m);
        var key = "Order-" + Guid.NewGuid().ToString("N");

        var first = await SendAsync(sender.Id, key, To(receiverWallet, 1000.00m));
        var second = await SendAsync(sender.Id, key.ToLowerInvariant(), To(receiverWallet, 1000.00m));

        Assert.True(first.Succeeded);
        Assert.True(second.Replayed);
        Assert.Equal(first.Value, second.Value);
        Assert.Equal(1, await CountTransfersAsync(senderWallet, TransactionStatus.Completed));
    }

    [Theory]
    [InlineData("with space")]
    [InlineData("k\u00e9y")]
    public async Task A_key_with_other_characters_is_a_validation_error_and_nothing_is_written(string key)
    {
        var (sender, senderWallet) = await CustomerAsync(5000.00m);
        var (_, receiverWallet) = await CustomerAsync(0m);

        var result = await SendAsync(sender.Id, key, To(receiverWallet, 1000.00m));

        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        Assert.Equal(0, await CountTransfersAsync(senderWallet, TransactionStatus.Completed));
    }

    [Fact]
    public async Task A_commit_that_succeeds_but_reports_an_error_is_retried_into_a_replay_and_posts_once()
    {
        var (sender, senderWallet) = await CustomerAsync(5000.00m);
        var (_, receiverWallet) = await CustomerAsync(0m);
        await using var db = FaultyContext.Create(sql.AdminConnectionString, new FailAfterCommitOnce());

        var result = await TestServices.Transfers(db).TransferAsync(
            sender.Id, TestServices.NewKey(), To(receiverWallet, 1000.00m), Caller, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.True(result.Replayed);
        Assert.Equal(1, await CountTransfersAsync(senderWallet, TransactionStatus.Completed));
        Assert.Equal(3990.00m, await BalanceAsync(senderWallet));
        Assert.Equal(1000.00m, await BalanceAsync(receiverWallet));
    }

    private async Task<User> NewUserWithoutWalletAsync()
    {
        await using var db = sql.NewContext();
        var user = new User
        {
            Email = $"nowallet.{Guid.NewGuid():N}@example.com",
            Phone = "+94700" + Random.Shared.Next(100_000, 999_999),
            FullName = "Dilani Senanayake",
            PasswordHash = "hash-not-used-in-these-tests",
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return user;
    }
}
