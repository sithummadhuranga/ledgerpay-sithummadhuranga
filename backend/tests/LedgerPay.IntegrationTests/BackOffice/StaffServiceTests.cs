using LedgerPay.Application.Admin;
using LedgerPay.Application.Auth;
using LedgerPay.Application.Common;
using LedgerPay.Domain.Constants;
using LedgerPay.Domain.Entities;
using LedgerPay.Infrastructure.Seeding;
using LedgerPay.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace LedgerPay.IntegrationTests.BackOffice;

public class StaffServiceTests(SqlServerFixture sql)
{
    private const string Password = "Kandy-Lake-2026!";

    private static readonly RequestInfo Caller = new("203.0.113.90", "corr-staff-test", "Chrome on macOS");

    private async Task<User> NewOperatorAsync()
    {
        await using var db = sql.NewContext();
        return await TestData.AddStaffAsync(db, RoleNames.Operator, Password);
    }

    private async Task<Guid> AdminIdAsync()
    {
        await using var db = sql.NewContext();
        return await db.Users.Where(user => user.Email == SeedData.AdminEmail).Select(user => user.Id).SingleAsync(TestContext.Current.CancellationToken);
    }

    private async Task<ServiceResult<StaffMember>> SetAsync(Guid actor, string email, bool? restricted, string? reason = "Left the company", TimeProvider? clock = null)
    {
        await using var db = sql.NewContext();
        return await TestServices.Staff(db, clock).SetRestrictionAsync(
            actor, new StaffRestrictionRequest { Email = email, Restricted = restricted, Reason = reason }, Caller, CancellationToken.None);
    }

    private async Task<User> ReloadAsync(Guid id)
    {
        await using var db = sql.NewContext();
        return await db.Users.AsNoTracking().SingleAsync(user => user.Id == id, TestContext.Current.CancellationToken);
    }

    // ---- the list

    [Fact]
    public async Task The_list_has_operators_and_admins_and_no_customer()
    {
        var staff = await NewOperatorAsync();
        await using var db = sql.NewContext();
        var (customer, _, _) = await TestData.AddCustomerAsync(db);

        var list = await TestServices.Staff(db).ListAsync(CancellationToken.None);

        Assert.Contains(list, member => member.Email == staff.Email && member.Role == RoleNames.Operator && !member.Restricted);
        Assert.Contains(list, member => member.Email == SeedData.AdminEmail && member.Role == RoleNames.Admin);
        Assert.Contains(list, member => member.Email == SeedData.OperatorEmail);
        Assert.DoesNotContain(list, member => member.Email == customer.Email);
    }

    // ---- restricting

    [Fact]
    public async Task Restricting_an_operator_records_who_why_and_when()
    {
        var target = await NewOperatorAsync();
        var admin = await AdminIdAsync();
        var clock = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(DateTimeOffset.UtcNow);

        var result = await SetAsync(admin, target.Email, true, "  Shared the password  ", clock);

        Assert.True(result.Succeeded);
        var member = result.Value!;
        Assert.Equal((true, "Shared the password", "Chamara Rajapaksa", RoleNames.Operator), (member.Restricted, member.RestrictedReason, member.RestrictedBy, member.Role));
        var stored = await ReloadAsync(target.Id);
        Assert.Equal(clock.GetUtcNow().UtcDateTime, stored.RestrictedAt);
        Assert.Equal(admin, stored.RestrictedByUserId);
        Assert.Equal("Shared the password", stored.RestrictedReason);
    }

    [Fact]
    public async Task The_email_is_found_whatever_its_case_and_spaces()
    {
        var target = await NewOperatorAsync();

        var result = await SetAsync(await AdminIdAsync(), $"  {target.Email.ToUpperInvariant()}  ", true);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Restricting_ends_every_session_of_the_operator_and_leaves_other_people_alone()
    {
        var target = await NewOperatorAsync();
        var bystander = await NewOperatorAsync();
        var clock = TimeProvider.System;
        string firstToken, secondToken, bystanderToken;
        await using (var db = sql.NewContext())
        {
            var auth = TestServices.Auth(db, clock);
            firstToken = (await auth.LoginAsync(new LoginRequest(target.Email, Password), Caller, CancellationToken.None)).Value!.Refresh.Token;
        }

        await using (var db = sql.NewContext())
        {
            secondToken = (await TestServices.Auth(db, clock).LoginAsync(new LoginRequest(target.Email, Password), Caller, CancellationToken.None)).Value!.Refresh.Token;
            bystanderToken = (await TestServices.Auth(db, clock).LoginAsync(new LoginRequest(bystander.Email, Password), Caller, CancellationToken.None)).Value!.Refresh.Token;
        }

        await SetAsync(await AdminIdAsync(), target.Email, true);

        // The rows themselves are checked, because the refresh below would also refuse a restricted account.
        await using var check = sql.NewContext();
        var rows = await check.RefreshTokens.AsNoTracking().Where(row => row.UserId == target.Id).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, rows.Count);
        Assert.All(rows, row => Assert.NotNull(row.RevokedAt));
        Assert.Single(await check.RefreshTokens.AsNoTracking().Where(row => row.UserId == bystander.Id && row.RevokedAt == null).ToListAsync(TestContext.Current.CancellationToken));
        var sessions = TestServices.Sessions(check, clock);
        Assert.Equal(ErrorCodes.InvalidRefreshToken, (await sessions.RefreshAsync(firstToken, Caller, CancellationToken.None)).ErrorCode);
        Assert.Equal(ErrorCodes.InvalidRefreshToken, (await sessions.RefreshAsync(secondToken, Caller, CancellationToken.None)).ErrorCode);
        Assert.True((await sessions.RefreshAsync(bystanderToken, Caller, CancellationToken.None)).Succeeded);
    }

    [Fact]
    public async Task A_restriction_and_its_lifting_are_in_the_audit_log_with_the_reason()
    {
        var target = await NewOperatorAsync();
        var admin = await AdminIdAsync();

        await SetAsync(admin, target.Email, true, "Suspected misuse");
        await SetAsync(admin, target.Email, false, "Cleared after review");

        await using var db = sql.NewContext();
        var entries = await db.AuditLogs.AsNoTracking()
            .Where(log => log.ActorUserId == admin && log.Details!.StartsWith(target.FullName + ":"))
            .OrderBy(log => log.CreatedAt).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal([AuditActions.AccountRestricted, AuditActions.AccountRestrictionLifted], entries.Select(entry => entry.Action).ToArray());
        Assert.All(entries, entry => Assert.Equal((admin, AuditEntityTypes.User, "203.0.113.90", "corr-staff-test"), (entry.ActorUserId!.Value, entry.EntityType, entry.IpAddress, entry.CorrelationId)));
        Assert.EndsWith("Suspected misuse", entries[0].Details);
        Assert.EndsWith("Cleared after review", entries[1].Details);
        Assert.DoesNotContain(target.Email, entries[0].Details!);
        Assert.All(entries, entry => Assert.Null(entry.EntityReference));
        Assert.DoesNotContain(target.Id.ToString(), entries[0].Details!);
    }

    [Fact]
    public async Task Lifting_a_restriction_clears_it_and_the_operator_can_sign_in_again()
    {
        var target = await NewOperatorAsync();
        var admin = await AdminIdAsync();
        await SetAsync(admin, target.Email, true);

        var lifted = await SetAsync(admin, target.Email, false, "Cleared");

        Assert.True(lifted.Succeeded);
        Assert.False(lifted.Value!.Restricted);
        var stored = await ReloadAsync(target.Id);
        Assert.Equal((null, null, null), (stored.RestrictedAt, stored.RestrictedReason, stored.RestrictedByUserId));
        await using var db = sql.NewContext();
        Assert.True((await TestServices.Auth(db).LoginAsync(new LoginRequest(target.Email, Password), Caller, CancellationToken.None)).Succeeded);
    }

    [Fact]
    public async Task Setting_the_state_the_account_is_already_in_is_a_conflict_and_changes_nothing()
    {
        var target = await NewOperatorAsync();
        var admin = await AdminIdAsync();

        var liftNothing = await SetAsync(admin, target.Email, false);
        await SetAsync(admin, target.Email, true, "First reason");
        var again = await SetAsync(admin, target.Email, true, "Second reason");

        Assert.Equal(ErrorCodes.AccountAlreadyInState, liftNothing.ErrorCode);
        Assert.Equal(ErrorCodes.AccountAlreadyInState, again.ErrorCode);
        Assert.Equal("First reason", (await ReloadAsync(target.Id)).RestrictedReason);
    }

    [Fact]
    public async Task An_admin_cannot_be_restricted_and_nobody_can_restrict_themselves()
    {
        var admin = await AdminIdAsync();

        var ofAdmin = await SetAsync(admin, SeedData.AdminEmail, true);
        await using var db = sql.NewContext();
        var secondAdmin = await TestData.AddStaffAsync(db, RoleNames.Admin, Password);
        var ofOtherAdmin = await SetAsync(admin, secondAdmin.Email, true);

        Assert.Equal(ErrorCodes.StaffNotRestrictable, ofAdmin.ErrorCode);
        Assert.Equal(ErrorCodes.StaffNotRestrictable, ofOtherAdmin.ErrorCode);
        Assert.Null((await ReloadAsync(secondAdmin.Id)).RestrictedAt);
    }

    [Fact]
    public async Task A_customer_and_an_unknown_email_are_not_staff_to_this_screen()
    {
        await using var db = sql.NewContext();
        var (customer, _, _) = await TestData.AddCustomerAsync(db);
        var admin = await AdminIdAsync();

        var ofCustomer = await SetAsync(admin, customer.Email, true);
        var ofNobody = await SetAsync(admin, "nobody.here@example.com", true);

        Assert.Equal(ErrorCodes.StaffNotFound, ofCustomer.ErrorCode);
        Assert.Equal(ErrorCodes.StaffNotFound, ofNobody.ErrorCode);
        Assert.Null((await ReloadAsync(customer.Id)).RestrictedAt);
    }

    [Fact]
    public async Task A_request_without_the_flag_changes_nothing()
    {
        var target = await NewOperatorAsync();
        await SetAsync(await AdminIdAsync(), target.Email, true);

        var result = await SetAsync(await AdminIdAsync(), target.Email, null);

        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        Assert.NotNull((await ReloadAsync(target.Id)).RestrictedAt);
    }

    [Fact]
    public async Task Two_admins_acting_at_once_restrict_the_account_once()
    {
        var target = await NewOperatorAsync();
        var admin = await AdminIdAsync();

        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => SetAsync(admin, target.Email, true)));

        Assert.Equal(1, results.Count(result => result.Succeeded));
        Assert.All(results.Where(result => !result.Succeeded), result => Assert.Equal(ErrorCodes.AccountAlreadyInState, result.ErrorCode));
        await using var db = sql.NewContext();
        Assert.Equal(1, await db.AuditLogs.CountAsync(log => log.Details!.StartsWith(target.FullName + ":") && log.Action == AuditActions.AccountRestricted, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_refresh_and_a_restriction_of_the_same_account_take_turns_on_the_user_row()
    {
        var target = await NewOperatorAsync();
        var clock = TimeProvider.System;
        string token;
        await using (var db = sql.NewContext())
        {
            token = (await TestServices.Auth(db, clock).LoginAsync(new LoginRequest(target.Email, Password), Caller, CancellationToken.None)).Value!.Refresh.Token;
        }

        await using var holder = sql.NewContext();
        await using var refreshing = sql.NewContext();
        Task<ServiceResult<Refreshed>> refresh;
        await using (var transaction = await holder.Database.BeginTransactionAsync(TestContext.Current.CancellationToken))
        {
            await holder.LockUserAsync(target.Id, TestContext.Current.CancellationToken);
            refresh = TestServices.Sessions(refreshing, clock).RefreshAsync(token, Caller, CancellationToken.None);
            await Task.Delay(TimeSpan.FromSeconds(1.5), TestContext.Current.CancellationToken);
            Assert.False(refresh.IsCompleted, "The refresh did not wait for the user row.");
        }

        Assert.True((await refresh).Succeeded);
    }

    [Fact]
    public async Task A_refresh_and_a_restriction_at_once_never_leave_a_live_session_behind()
    {
        var admin = await AdminIdAsync();
        foreach (var _ in Enumerable.Range(0, 8))
        {
            var target = await NewOperatorAsync();
            string token;
            await using (var db = sql.NewContext())
            {
                token = (await TestServices.Auth(db).LoginAsync(new LoginRequest(target.Email, Password), Caller, CancellationToken.None)).Value!.Refresh.Token;
            }

            await Task.WhenAll(
                Task.Run(async () =>
                {
                    await using var db = sql.NewContext();
                    await TestServices.Sessions(db).RefreshAsync(token, Caller, CancellationToken.None);
                }, TestContext.Current.CancellationToken),
                SetAsync(admin, target.Email, true));

            await using var check = sql.NewContext();
            Assert.Empty(await check.RefreshTokens.AsNoTracking().Where(row => row.UserId == target.Id && row.RevokedAt == null).ToListAsync(TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task A_token_replaced_a_moment_ago_does_not_get_an_access_token_for_a_restricted_account()
    {
        var target = await NewOperatorAsync();
        var clock = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(DateTimeOffset.UtcNow);
        string first;
        await using (var db = sql.NewContext())
        {
            first = (await TestServices.Auth(db, clock).LoginAsync(new LoginRequest(target.Email, Password), Caller, CancellationToken.None)).Value!.Refresh.Token;
        }

        await using (var db = sql.NewContext())
        {
            await TestServices.Sessions(db, clock).RefreshAsync(first, Caller, CancellationToken.None);
        }

        await SetAsync(await AdminIdAsync(), target.Email, true, clock: clock);
        clock.Advance(TimeSpan.FromSeconds(2));

        await using var check = sql.NewContext();
        var result = await TestServices.Sessions(check, clock).RefreshAsync(first, Caller, CancellationToken.None);

        Assert.Equal(ErrorCodes.InvalidRefreshToken, result.ErrorCode);
    }

    // ---- what a restriction does to sign-in

    [Fact]
    public async Task A_restricted_operator_who_knows_the_password_is_told_the_account_is_restricted()
    {
        var target = await NewOperatorAsync();
        await SetAsync(await AdminIdAsync(), target.Email, true);

        await using var db = sql.NewContext();
        var result = await TestServices.Auth(db).LoginAsync(new LoginRequest(target.Email, Password), Caller, CancellationToken.None);

        Assert.Equal(ErrorCodes.AccountRestricted, result.ErrorCode);
        await using var check = sql.NewContext();
        Assert.Empty(await check.RefreshTokens.Where(row => row.UserId == target.Id && row.RevokedAt == null).ToListAsync(TestContext.Current.CancellationToken));
        Assert.True(await check.AuditLogs.AnyAsync(log => log.ActorUserId == target.Id && log.Action == AuditActions.LoginFailed && log.Details == "Account is restricted", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Someone_who_does_not_know_the_password_learns_nothing_about_the_restriction()
    {
        var target = await NewOperatorAsync();
        await SetAsync(await AdminIdAsync(), target.Email, true);

        await using var db = sql.NewContext();
        var result = await TestServices.Auth(db).LoginAsync(new LoginRequest(target.Email, "Wrong-Password-1!"), Caller, CancellationToken.None);

        Assert.Equal(ErrorCodes.InvalidCredentials, result.ErrorCode);
    }
}
