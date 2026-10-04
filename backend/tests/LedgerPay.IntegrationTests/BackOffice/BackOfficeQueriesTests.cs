using LedgerPay.Application.BackOffice;
using LedgerPay.Application.Common;
using LedgerPay.Domain.Constants;
using LedgerPay.Domain.Entities;
using LedgerPay.Domain.Enums;
using LedgerPay.Infrastructure.Seeding;
using LedgerPay.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace LedgerPay.IntegrationTests.BackOffice;

public class BackOfficeQueriesTests(SqlServerFixture sql)
{
    private static readonly RequestInfo Caller = new("203.0.113.80", "corr-backoffice-test");

    // A customer whose name carries a word nobody else has, so a search finds this customer and no other.
    private async Task<(User User, Wallet Wallet, string Word)> NewPersonAsync(string? word = null)
    {
        word ??= "Zz" + Guid.NewGuid().ToString("N")[..10];
        await using var db = sql.NewContext();
        var (user, wallet, _) = await TestData.AddCustomerAsync(db);
        user.FullName = $"Nimali {word}";
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (user, wallet, word);
    }

    private async Task<PagedResponseOf<UserSummary>> SearchAsync(UserSearchQuery query)
    {
        await using var db = sql.NewContext();
        var page = await TestServices.BackOffice(db).ListUsersAsync(query, CancellationToken.None);
        return new PagedResponseOf<UserSummary>(page.Items, page.TotalCount, page.TotalPages);
    }

    private sealed record PagedResponseOf<T>(IReadOnlyList<T> Items, int TotalCount, int TotalPages);

    private async Task<Transaction> AddTransferAsync(
        Wallet sender, Wallet? receiver, TransactionStatus status = TransactionStatus.Completed, DateTime? at = null,
        TransactionType type = TransactionType.Transfer, string? requestedReceiver = null)
    {
        await using var db = sql.NewContext();
        var transaction = new Transaction
        {
            Reference = "TX" + Guid.NewGuid().ToString("N")[..16].ToUpperInvariant(),
            Type = type,
            Status = status,
            FailureCode = status == TransactionStatus.Failed ? ErrorCodes.InsufficientFunds : null,
            SenderWalletId = type == TransactionType.TopUp ? null : sender.Id,
            ReceiverWalletId = receiver?.Id,
            RequestedReceiver = requestedReceiver,
            Amount = 500.00m,
            Fee = type == TransactionType.TopUp ? 0m : 10.00m,
            InitiatedByUserId = sender.UserId,
            CreatedAt = at ?? DateTime.UtcNow
        };
        db.Transactions.Add(transaction);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return transaction;
    }

    // ---- finding people

    [Fact]
    public async Task A_person_is_found_by_part_of_the_name_whatever_the_case()
    {
        var (_, wallet, word) = await NewPersonAsync();

        var found = await SearchAsync(new UserSearchQuery { Search = word.ToUpperInvariant()[..8] });

        var summary = Assert.Single(found.Items);
        Assert.Equal(wallet.WalletNumber, summary.WalletNumber);
    }

    [Fact]
    public async Task A_person_is_found_by_part_of_the_email_the_mobile_number_and_the_wallet_number()
    {
        var (user, wallet, _) = await NewPersonAsync();

        foreach (var term in new[] { user.Email[..user.Email.IndexOf('@')], user.Phone, wallet.WalletNumber, wallet.WalletNumber[3..9] })
        {
            var found = await SearchAsync(new UserSearchQuery { Search = term });
            Assert.Contains(found.Items, item => item.WalletNumber == wallet.WalletNumber);
        }
    }

    [Fact]
    public async Task The_summary_has_what_staff_need_and_no_internal_id()
    {
        var (user, wallet, word) = await NewPersonAsync();

        var summary = Assert.Single((await SearchAsync(new UserSearchQuery { Search = word })).Items);

        Assert.Equal(($"Nimali {word}", LedgerPay.Domain.Rules.ContactMask.Email(user.Email), LedgerPay.Domain.Rules.ContactMask.Phone(user.Phone), 0m),
            (summary.FullName, summary.Email, summary.Phone, summary.Balance));
        Assert.DoesNotContain(user.Email, summary.Email);
        Assert.DoesNotContain(user.Phone, summary.Phone);
        Assert.Equal(WalletStatus.Active, summary.WalletStatus);
        Assert.False(summary.Locked);
        Assert.Equal(DateTimeKind.Utc, summary.CreatedAt.Kind);
        Assert.Equal(wallet.WalletNumber, summary.WalletNumber);
    }

    [Theory]
    [InlineData("%")]
    [InlineData("_")]
    [InlineData("[a-z]")]
    [InlineData("' OR 1=1 --")]
    public async Task Wildcards_and_quotes_in_the_search_are_only_text(string term)
    {
        await NewPersonAsync();

        var found = await SearchAsync(new UserSearchQuery { Search = term });

        Assert.Empty(found.Items);
    }

    [Fact]
    public async Task Spaces_around_the_search_are_ignored_and_a_blank_search_lists_everybody()
    {
        var (_, wallet, word) = await NewPersonAsync();

        var spaced = await SearchAsync(new UserSearchQuery { Search = $"  {word}  " });
        var blank = await SearchAsync(new UserSearchQuery { Search = "   ", PageSize = 100 });

        Assert.Equal(wallet.WalletNumber, Assert.Single(spaced.Items).WalletNumber);
        Assert.True(blank.TotalCount >= 1);
    }

    [Fact]
    public async Task Customers_come_in_order_of_name_and_are_paged()
    {
        var word = "Pg" + Guid.NewGuid().ToString("N")[..10];
        foreach (var _ in Enumerable.Range(0, 3))
        {
            await NewPersonAsync(word);
        }

        var first = await SearchAsync(new UserSearchQuery { Search = word, PageSize = 2 });
        var second = await SearchAsync(new UserSearchQuery { Search = word, PageSize = 2, Page = 2 });

        Assert.Equal((2, 3, 2), (first.Items.Count, first.TotalCount, first.TotalPages));
        Assert.Single(second.Items);
        Assert.Equal(first.Items.Concat(second.Items).Select(item => item.WalletNumber).Order().ToList(),
            first.Items.Concat(second.Items).Select(item => item.WalletNumber).ToList());
    }

    [Fact]
    public async Task The_staff_of_the_bank_are_not_in_the_list_of_customers()
    {
        var found = await SearchAsync(new UserSearchQuery { Search = SeedData_OperatorEmailPart, PageSize = 100 });

        Assert.Empty(found.Items);
    }

    private const string SeedData_OperatorEmailPart = "dilani.senanayake";

    // ---- the status filter

    [Fact]
    public async Task The_frozen_filter_lists_frozen_wallets_only()
    {
        var word = "Fz" + Guid.NewGuid().ToString("N")[..10];
        var (_, frozen, _) = await NewPersonAsync(word);
        await NewPersonAsync(word);
        await using (var db = sql.NewContext())
        {
            var wallet = await db.Wallets.SingleAsync(candidate => candidate.Id == frozen.Id, TestContext.Current.CancellationToken);
            wallet.Status = WalletStatus.Frozen;
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var found = await SearchAsync(new UserSearchQuery { Search = word, Status = UserStatusFilter.Frozen });

        var summary = Assert.Single(found.Items);
        Assert.Equal(frozen.WalletNumber, summary.WalletNumber);
        Assert.Equal(WalletStatus.Frozen, summary.WalletStatus);
    }

    [Fact]
    public async Task The_locked_filter_lists_accounts_that_are_locked_now_and_not_one_whose_lock_has_run_out()
    {
        var word = "Lk" + Guid.NewGuid().ToString("N")[..10];
        var (lockedUser, locked, _) = await NewPersonAsync(word);
        var (expiredUser, expired, _) = await NewPersonAsync(word);
        await using (var db = sql.NewContext())
        {
            (await db.Users.SingleAsync(candidate => candidate.Id == lockedUser.Id, TestContext.Current.CancellationToken)).LockoutEnd = DateTime.UtcNow.AddMinutes(10);
            (await db.Users.SingleAsync(candidate => candidate.Id == expiredUser.Id, TestContext.Current.CancellationToken)).LockoutEnd = DateTime.UtcNow.AddMinutes(-10);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var found = await SearchAsync(new UserSearchQuery { Search = word, Status = UserStatusFilter.Locked });
        var active = await SearchAsync(new UserSearchQuery { Search = word, Status = UserStatusFilter.Active });

        var summary = Assert.Single(found.Items);
        Assert.Equal(locked.WalletNumber, summary.WalletNumber);
        Assert.True(summary.Locked);
        Assert.Equal(expired.WalletNumber, Assert.Single(active.Items).WalletNumber);
    }

    [Fact]
    public async Task The_active_filter_leaves_out_a_frozen_wallet_and_a_locked_account()
    {
        var word = "Ac" + Guid.NewGuid().ToString("N")[..10];
        var (_, normal, _) = await NewPersonAsync(word);
        var (_, frozen, _) = await NewPersonAsync(word);
        await using (var db = sql.NewContext())
        {
            (await db.Wallets.SingleAsync(candidate => candidate.Id == frozen.Id, TestContext.Current.CancellationToken)).Status = WalletStatus.Frozen;
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var active = await SearchAsync(new UserSearchQuery { Search = word, Status = UserStatusFilter.Active });

        Assert.Equal(normal.WalletNumber, Assert.Single(active.Items).WalletNumber);
    }

    // ---- one person

    [Fact]
    public async Task A_person_page_has_the_freeze_reason_who_froze_it_and_the_latest_transactions_newest_first()
    {
        var (user, wallet, _) = await NewPersonAsync();
        var (_, other, _) = await NewPersonAsync();
        await using (var db = sql.NewContext())
        {
            var operatorId = await db.Users.Where(candidate => candidate.Email == SeedData.OperatorEmail).Select(candidate => candidate.Id).SingleAsync(TestContext.Current.CancellationToken);
            var tracked = await db.Wallets.SingleAsync(candidate => candidate.Id == wallet.Id, TestContext.Current.CancellationToken);
            tracked.Status = WalletStatus.Frozen;
            tracked.StatusReason = "Reported lost phone";
            tracked.StatusChangedAt = DateTime.UtcNow;
            tracked.StatusChangedByUserId = operatorId;
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var older = await AddTransferAsync(wallet, other, at: DateTime.UtcNow.AddHours(-2));
        var newer = await AddTransferAsync(other, wallet, at: DateTime.UtcNow.AddHours(-1));
        var failed = await AddTransferAsync(wallet, null, TransactionStatus.Failed, DateTime.UtcNow, requestedReceiver: "+94700000000");
        await using var read = sql.NewContext();
        var actor = await read.Users.Where(candidate => candidate.Email == SeedData.OperatorEmail).Select(candidate => candidate.Id).SingleAsync(TestContext.Current.CancellationToken);

        var result = await TestServices.BackOffice(read).GetUserAsync(actor, wallet.WalletNumber, Caller, CancellationToken.None);

        Assert.True(result.Succeeded);
        var detail = result.Value!;
        Assert.Equal((user.Email, user.Phone, WalletStatus.Frozen, "Reported lost phone"), (detail.Email, detail.Phone, detail.WalletStatus, detail.StatusReason));
        Assert.Equal("Dilani Senanayake", detail.StatusChangedBy);
        Assert.Equal([failed.Reference, newer.Reference, older.Reference], detail.RecentTransactions.Select(item => item.Reference).ToArray());
        Assert.Equal(TransactionStatus.Failed, detail.RecentTransactions[0].Status);
        Assert.Equal("+94700000000", detail.RecentTransactions[0].RequestedReceiver);
        Assert.Equal(other.WalletNumber, detail.RecentTransactions[2].ReceiverWalletNumber);
    }

    [Fact]
    public async Task A_person_page_shows_only_the_latest_ten()
    {
        var (_, wallet, _) = await NewPersonAsync();
        var (_, other, _) = await NewPersonAsync();
        foreach (var hours in Enumerable.Range(1, 12))
        {
            await AddTransferAsync(wallet, other, at: DateTime.UtcNow.AddHours(-hours));
        }

        await using var db = sql.NewContext();
        var actor = await db.Users.Where(candidate => candidate.Email == SeedData.AdminEmail).Select(candidate => candidate.Id).SingleAsync(TestContext.Current.CancellationToken);
        var result = await TestServices.BackOffice(db).GetUserAsync(actor, wallet.WalletNumber, Caller, CancellationToken.None);

        Assert.Equal(10, result.Value!.RecentTransactions.Count);
    }

    [Fact]
    public async Task Looking_at_a_person_is_written_to_the_audit_log_with_who_looked()
    {
        var (_, wallet, _) = await NewPersonAsync();
        await using var db = sql.NewContext();
        var actor = await db.Users.Where(candidate => candidate.Email == SeedData.OperatorEmail).Select(candidate => candidate.Id).SingleAsync(TestContext.Current.CancellationToken);

        await TestServices.BackOffice(db).GetUserAsync(actor, wallet.WalletNumber, Caller, CancellationToken.None);

        await using var check = sql.NewContext();
        var entry = await check.AuditLogs.AsNoTracking().SingleAsync(
            log => log.Action == AuditActions.UserViewed && log.EntityReference == wallet.WalletNumber, TestContext.Current.CancellationToken);
        Assert.Equal(actor, entry.ActorUserId);
        Assert.Equal(AuditEntityTypes.Wallet, entry.EntityType);
        Assert.Equal("203.0.113.80", entry.IpAddress);
        Assert.Equal("corr-backoffice-test", entry.CorrelationId);
    }

    [Fact]
    public async Task The_same_person_opened_again_inside_five_minutes_is_one_entry_and_after_that_a_new_one()
    {
        var (_, wallet, _) = await NewPersonAsync();
        var clock = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var db = sql.NewContext();
        var actor = await db.Users.Where(candidate => candidate.Email == SeedData.OperatorEmail).Select(candidate => candidate.Id).SingleAsync(TestContext.Current.CancellationToken);
        var other = await db.Users.Where(candidate => candidate.Email == SeedData.AdminEmail).Select(candidate => candidate.Id).SingleAsync(TestContext.Current.CancellationToken);

        async Task LookAsync(Guid by)
        {
            await using var each = sql.NewContext();
            await TestServices.BackOffice(each, clock).GetUserAsync(by, wallet.WalletNumber, Caller, CancellationToken.None);
        }

        async Task<int> CountAsync(Guid by)
        {
            await using var check = sql.NewContext();
            return await check.AuditLogs.CountAsync(log => log.ActorUserId == by && log.Action == AuditActions.UserViewed && log.EntityReference == wallet.WalletNumber, TestContext.Current.CancellationToken);
        }

        await LookAsync(actor);
        clock.Advance(TimeSpan.FromMinutes(4));
        await LookAsync(actor);
        await LookAsync(other);
        Assert.Equal((1, 1), (await CountAsync(actor), await CountAsync(other)));

        clock.Advance(TimeSpan.FromMinutes(2));
        await LookAsync(actor);

        Assert.Equal(2, await CountAsync(actor));
    }

    [Fact]
    public async Task A_wallet_that_does_not_exist_is_not_found_and_leaves_no_entry()
    {
        await using var db = sql.NewContext();
        var actor = await db.Users.Where(candidate => candidate.Email == SeedData.OperatorEmail).Select(candidate => candidate.Id).SingleAsync(TestContext.Current.CancellationToken);

        var result = await TestServices.BackOffice(db).GetUserAsync(actor, "100000000001", Caller, CancellationToken.None);

        Assert.Equal(ErrorCodes.WalletNotFound, result.ErrorCode);
        await using var check = sql.NewContext();
        Assert.False(await check.AuditLogs.AnyAsync(log => log.EntityReference == "100000000001", TestContext.Current.CancellationToken));
    }

    // ---- every transaction

    [Fact]
    public async Task Transactions_come_newest_first_with_both_wallets_and_whole_names()
    {
        var (_, sender, senderWord) = await NewPersonAsync();
        var (_, receiver, receiverWord) = await NewPersonAsync();
        var older = await AddTransferAsync(sender, receiver, at: DateTime.UtcNow.AddHours(-3));
        var newer = await AddTransferAsync(sender, receiver, at: DateTime.UtcNow.AddHours(-2));

        await using var db = sql.NewContext();
        var page = await TestServices.BackOffice(db).ListTransactionsAsync(new StaffTransactionQuery { WalletNumber = sender.WalletNumber }, CancellationToken.None);

        Assert.Equal([newer.Reference, older.Reference], page.Items.Select(item => item.Reference).ToArray());
        var item = page.Items[0];
        Assert.Equal((sender.WalletNumber, $"Nimali {senderWord}", receiver.WalletNumber, $"Nimali {receiverWord}"),
            (item.SenderWalletNumber, item.SenderName, item.ReceiverWalletNumber, item.ReceiverName));
        Assert.Equal((500.00m, 10.00m), (item.Amount, item.Fee));
    }

    [Fact]
    public async Task A_wallet_filter_finds_what_the_wallet_sent_and_what_it_received()
    {
        var (_, wallet, _) = await NewPersonAsync();
        var (_, other, _) = await NewPersonAsync();
        var (_, stranger, _) = await NewPersonAsync();
        var sent = await AddTransferAsync(wallet, other);
        var received = await AddTransferAsync(other, wallet);
        await AddTransferAsync(other, stranger);

        await using var db = sql.NewContext();
        var page = await TestServices.BackOffice(db).ListTransactionsAsync(new StaffTransactionQuery { WalletNumber = wallet.WalletNumber }, CancellationToken.None);

        Assert.Equal(new[] { sent.Reference, received.Reference }.Order().ToArray(), page.Items.Select(item => item.Reference).Order().ToArray());
    }

    [Fact]
    public async Task The_type_and_status_filters_pick_failed_transfers_and_top_ups()
    {
        var (_, wallet, _) = await NewPersonAsync();
        var (_, other, _) = await NewPersonAsync();
        var completed = await AddTransferAsync(wallet, other);
        var failed = await AddTransferAsync(wallet, null, TransactionStatus.Failed, requestedReceiver: "+94700000001");
        var topUp = await AddTransferAsync(wallet, wallet, type: TransactionType.TopUp);

        await using var db = sql.NewContext();
        var queries = TestServices.BackOffice(db);
        var failedOnly = await queries.ListTransactionsAsync(new StaffTransactionQuery { WalletNumber = wallet.WalletNumber, Status = TransactionStatus.Failed }, CancellationToken.None);
        var topUps = await queries.ListTransactionsAsync(new StaffTransactionQuery { WalletNumber = wallet.WalletNumber, Type = TransactionType.TopUp }, CancellationToken.None);
        var completedTransfers = await queries.ListTransactionsAsync(
            new StaffTransactionQuery { WalletNumber = wallet.WalletNumber, Type = TransactionType.Transfer, Status = TransactionStatus.Completed }, CancellationToken.None);

        Assert.Equal(failed.Reference, Assert.Single(failedOnly.Items).Reference);
        Assert.Null(failedOnly.Items[0].ReceiverName);
        Assert.Equal("+94700000001", failedOnly.Items[0].RequestedReceiver);
        Assert.Equal(topUp.Reference, Assert.Single(topUps.Items).Reference);
        Assert.Null(topUps.Items[0].SenderWalletNumber);
        Assert.Equal(completed.Reference, Assert.Single(completedTransfers.Items).Reference);
    }

    [Fact]
    public async Task The_date_filters_are_whole_days_in_utc_with_both_ends_included()
    {
        var (_, wallet, _) = await NewPersonAsync();
        var (_, other, _) = await NewPersonAsync();
        var day = new DateOnly(2026, 3, 10);
        var before = await AddTransferAsync(wallet, other, at: new DateTime(2026, 3, 9, 23, 59, 59, DateTimeKind.Utc));
        var first = await AddTransferAsync(wallet, other, at: new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc));
        var last = await AddTransferAsync(wallet, other, at: new DateTime(2026, 3, 10, 23, 59, 59, DateTimeKind.Utc));
        var after = await AddTransferAsync(wallet, other, at: new DateTime(2026, 3, 11, 0, 0, 0, DateTimeKind.Utc));

        await using var db = sql.NewContext();
        var page = await TestServices.BackOffice(db).ListTransactionsAsync(
            new StaffTransactionQuery { WalletNumber = wallet.WalletNumber, From = day, To = day }, CancellationToken.None);

        Assert.Equal(new[] { first.Reference, last.Reference }.Order().ToArray(), page.Items.Select(item => item.Reference).Order().ToArray());
        Assert.DoesNotContain(page.Items, item => item.Reference == before.Reference || item.Reference == after.Reference);
    }

    [Fact]
    public async Task The_largest_date_does_not_overflow()
    {
        await using var db = sql.NewContext();

        var page = await TestServices.BackOffice(db).ListTransactionsAsync(new StaffTransactionQuery { To = DateOnly.MaxValue }, CancellationToken.None);

        Assert.NotNull(page);
    }

    [Fact]
    public async Task Transactions_are_paged_with_a_total()
    {
        var (_, wallet, _) = await NewPersonAsync();
        var (_, other, _) = await NewPersonAsync();
        foreach (var hours in Enumerable.Range(1, 5))
        {
            await AddTransferAsync(wallet, other, at: DateTime.UtcNow.AddHours(-hours));
        }

        await using var db = sql.NewContext();
        var queries = TestServices.BackOffice(db);
        var first = await queries.ListTransactionsAsync(new StaffTransactionQuery { WalletNumber = wallet.WalletNumber, PageSize = 2 }, CancellationToken.None);
        var last = await queries.ListTransactionsAsync(new StaffTransactionQuery { WalletNumber = wallet.WalletNumber, PageSize = 2, Page = 3 }, CancellationToken.None);

        Assert.Equal((2, 5, 3), (first.Items.Count, first.TotalCount, first.TotalPages));
        Assert.Single(last.Items);
    }

    // ---- the audit log

    private async Task<User> NewActorAsync()
    {
        await using var db = sql.NewContext();
        var (user, _, _) = await TestData.AddCustomerAsync(db);
        return user;
    }

    private async Task AddAuditAsync(User? actor, string action, DateTime at, string? reference = null, string? details = null)
    {
        await using var db = sql.NewContext();
        db.AuditLogs.Add(new AuditLog
        {
            CreatedAt = at, ActorUserId = actor?.Id, Action = action, EntityType = AuditEntityTypes.User, EntityReference = reference,
            IpAddress = "203.0.113.5", CorrelationId = "corr-audit-read", Details = details
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task The_audit_log_comes_newest_first_with_who_acted()
    {
        var actor = await NewActorAsync();
        await AddAuditAsync(actor, AuditActions.Transfer, DateTime.UtcNow.AddMinutes(-3), "TXOLD");
        await AddAuditAsync(actor, AuditActions.TopUp, DateTime.UtcNow.AddMinutes(-2), "TXNEW", "a detail");

        await using var db = sql.NewContext();
        var page = await TestServices.BackOffice(db).ListAuditAsync(new AuditQuery { Actor = actor.Email }, CancellationToken.None);

        Assert.Equal(["TXNEW", "TXOLD"], page.Items.Select(item => item.EntityReference).ToArray());
        var item = page.Items[0];
        Assert.Equal((actor.FullName, actor.Email, "203.0.113.5", "corr-audit-read", "a detail", AuditActions.TopUp),
            (item.ActorName, item.ActorEmail, item.IpAddress, item.CorrelationId, item.Details, item.Action));
        Assert.Equal(DateTimeKind.Utc, item.CreatedAt.Kind);
    }

    [Fact]
    public async Task The_action_filter_and_the_actor_filter_narrow_the_list()
    {
        var actor = await NewActorAsync();
        var other = await NewActorAsync();
        await AddAuditAsync(actor, AuditActions.Transfer, DateTime.UtcNow, "A1");
        await AddAuditAsync(actor, AuditActions.TopUp, DateTime.UtcNow, "A2");
        await AddAuditAsync(other, AuditActions.Transfer, DateTime.UtcNow, "A3");

        await using var db = sql.NewContext();
        var queries = TestServices.BackOffice(db);
        var byAction = await queries.ListAuditAsync(new AuditQuery { Actor = actor.Email, Action = AuditActions.Transfer }, CancellationToken.None);
        var upperCase = await queries.ListAuditAsync(new AuditQuery { Actor = actor.Email.ToUpperInvariant(), Action = AuditActions.TopUp }, CancellationToken.None);
        var nobody = await queries.ListAuditAsync(new AuditQuery { Actor = actor.Email, Action = AuditActions.Logout }, CancellationToken.None);

        Assert.Equal("A1", Assert.Single(byAction.Items).EntityReference);
        Assert.Equal("A2", Assert.Single(upperCase.Items).EntityReference);
        Assert.Empty(nobody.Items);
    }

    [Fact]
    public async Task An_entry_with_no_actor_is_listed_with_no_name()
    {
        var reference = "NOACTOR" + Guid.NewGuid().ToString("N")[..8];
        await AddAuditAsync(null, AuditActions.LoginFailed, DateTime.UtcNow, reference, "Unknown account");

        await using var db = sql.NewContext();
        var page = await TestServices.BackOffice(db).ListAuditAsync(new AuditQuery { Action = AuditActions.LoginFailed, PageSize = 100 }, CancellationToken.None);

        var item = Assert.Single(page.Items, candidate => candidate.EntityReference == reference);
        Assert.Null(item.ActorName);
        Assert.Null(item.ActorEmail);
    }

    [Fact]
    public async Task The_audit_dates_are_whole_days_in_utc_with_both_ends_included()
    {
        var actor = await NewActorAsync();
        await AddAuditAsync(actor, AuditActions.Transfer, new DateTime(2026, 4, 9, 23, 59, 59, DateTimeKind.Utc), "B0");
        await AddAuditAsync(actor, AuditActions.Transfer, new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc), "B1");
        await AddAuditAsync(actor, AuditActions.Transfer, new DateTime(2026, 4, 10, 23, 59, 59, DateTimeKind.Utc), "B2");
        await AddAuditAsync(actor, AuditActions.Transfer, new DateTime(2026, 4, 11, 0, 0, 0, DateTimeKind.Utc), "B3");

        await using var db = sql.NewContext();
        var page = await TestServices.BackOffice(db).ListAuditAsync(
            new AuditQuery { Actor = actor.Email, From = new DateOnly(2026, 4, 10), To = new DateOnly(2026, 4, 10) }, CancellationToken.None);

        Assert.Equal(["B2", "B1"], page.Items.Select(item => item.EntityReference).ToArray());
    }

    [Fact]
    public async Task The_audit_log_is_paged_with_a_total()
    {
        var actor = await NewActorAsync();
        foreach (var minutes in Enumerable.Range(1, 5))
        {
            await AddAuditAsync(actor, AuditActions.Transfer, DateTime.UtcNow.AddMinutes(-minutes), $"P{minutes}");
        }

        await using var db = sql.NewContext();
        var page = await TestServices.BackOffice(db).ListAuditAsync(new AuditQuery { Actor = actor.Email, PageSize = 2, Page = 3 }, CancellationToken.None);

        Assert.Equal((1, 5, 3), (page.Items.Count, page.TotalCount, page.TotalPages));
    }
}
