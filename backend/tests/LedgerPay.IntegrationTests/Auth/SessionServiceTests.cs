using LedgerPay.Application.Auth;
using LedgerPay.Application.Common;
using LedgerPay.Domain.Constants;
using LedgerPay.Domain.Entities;
using LedgerPay.Domain.Rules;
using LedgerPay.Infrastructure.Security;
using LedgerPay.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace LedgerPay.IntegrationTests.Auth;

public class SessionServiceTests(SqlServerFixture sql)
{
    private static readonly RequestInfo Caller = new("203.0.113.70", "corr-session-test", "Chrome on macOS");
    private static readonly RequestInfo OtherDevice = new("198.51.100.9", "corr-session-other", "Safari on iPhone");

    private static FakeTimeProvider NewClock() => new(DateTimeOffset.UtcNow);

    private static DateTime Now(FakeTimeProvider clock) => clock.GetUtcNow().UtcDateTime;

    // A customer who exists but has not signed in.
    private async Task<(Guid UserId, string Email)> NewUserAsync()
    {
        var email = ApiCalls.NewEmail();
        await using var db = sql.NewContext();
        var registered = await TestServices.Auth(db).RegisterAsync(
            new RegisterRequest("Nimali Perera", email, ApiCalls.NewPhone(), ApiCalls.Password), Caller, CancellationToken.None);
        Assert.True(registered.Succeeded);
        return (await db.Users.AsNoTracking().Where(user => user.Email == email).Select(user => user.Id).SingleAsync(), email);
    }

    private async Task<SignedIn> SignInAsync(string email, TimeProvider clock, RequestInfo? from = null)
    {
        await using var db = sql.NewContext();
        var result = await TestServices.Auth(db, clock).LoginAsync(new LoginRequest(email, ApiCalls.Password), from ?? Caller, CancellationToken.None);
        Assert.True(result.Succeeded);
        return result.Value!;
    }

    private async Task<ServiceResult<Refreshed>> RefreshAsync(string? token, TimeProvider clock)
    {
        await using var db = sql.NewContext();
        return await TestServices.Sessions(db, clock).RefreshAsync(token, Caller, CancellationToken.None);
    }

    private async Task EndAsync(string? token, TimeProvider clock)
    {
        await using var db = sql.NewContext();
        await TestServices.Sessions(db, clock).EndAsync(token, Caller, CancellationToken.None);
    }

    private async Task<IReadOnlyList<SessionResponse>> ListAsync(Guid userId, string? token, TimeProvider clock)
    {
        await using var db = sql.NewContext();
        return await TestServices.Sessions(db, clock).ListAsync(userId, token, CancellationToken.None);
    }

    private async Task<ServiceResult<bool>> RevokeAsync(Guid userId, Guid sessionId, string? token, TimeProvider clock)
    {
        await using var db = sql.NewContext();
        return await TestServices.Sessions(db, clock).RevokeAsync(userId, sessionId, token, Caller, CancellationToken.None);
    }

    private async Task<List<RefreshToken>> RowsAsync(Guid userId)
    {
        await using var db = sql.NewContext();
        return await db.RefreshTokens.AsNoTracking().Where(row => row.UserId == userId).OrderBy(row => row.CreatedAt).ToListAsync();
    }

    private async Task<int> AuditCountAsync(Guid userId, string action)
    {
        await using var db = sql.NewContext();
        return await db.AuditLogs.CountAsync(log => log.ActorUserId == userId && log.Action == action);
    }

    // ---- starting

    [Fact]
    public async Task Signing_in_starts_a_session_and_keeps_only_the_hash_of_its_token()
    {
        var (userId, email) = await NewUserAsync();
        var clock = NewClock();

        var signedIn = await SignInAsync(email, clock);

        Assert.True(SessionRules.IsWellFormed(signedIn.Refresh.Token));
        var row = Assert.Single(await RowsAsync(userId));
        Assert.Equal(new RefreshTokenService().Hash(signedIn.Refresh.Token), row.TokenHash);
        Assert.NotEqual(signedIn.Refresh.Token, row.TokenHash);
        Assert.Equal(Now(clock) + SessionRules.IdleLifetime, row.ExpiresAt);
        Assert.Equal(row.ExpiresAt, signedIn.Refresh.ExpiresAt);
        Assert.Equal(row.CreatedAt, row.SessionStartedAt);
        Assert.Null(row.RevokedAt);
        Assert.Null(row.ReplacedById);
        Assert.Equal("203.0.113.70", row.IpAddress);
        Assert.Equal("Chrome on macOS", row.UserAgent);
    }

    [Fact]
    public async Task Each_sign_in_is_a_session_of_its_own()
    {
        var (userId, email) = await NewUserAsync();
        var clock = NewClock();

        var first = await SignInAsync(email, clock);
        var second = await SignInAsync(email, clock, OtherDevice);

        var rows = await RowsAsync(userId);
        Assert.Equal(2, rows.Count);
        Assert.NotEqual(rows[0].FamilyId, rows[1].FamilyId);
        Assert.NotEqual(first.Refresh.Token, second.Refresh.Token);
    }

    [Fact]
    public async Task A_sign_in_that_fails_starts_no_session()
    {
        var (userId, email) = await NewUserAsync();
        await using (var db = sql.NewContext())
        {
            var result = await TestServices.Auth(db).LoginAsync(new LoginRequest(email, "Wrong-Password-1!"), Caller, CancellationToken.None);
            Assert.False(result.Succeeded);
        }

        Assert.Empty(await RowsAsync(userId));
    }

    // ---- refreshing

    [Fact]
    public async Task Refreshing_replaces_the_token_and_keeps_the_session_going()
    {
        var (userId, email) = await NewUserAsync();
        var clock = NewClock();
        var signedIn = await SignInAsync(email, clock);
        clock.Advance(TimeSpan.FromHours(1));

        var result = await RefreshAsync(signedIn.Refresh.Token, clock);

        Assert.True(result.Succeeded);
        var refreshed = result.Value!;
        Assert.NotNull(refreshed.Refresh);
        Assert.True(SessionRules.IsWellFormed(refreshed.Refresh.Token));
        Assert.NotEqual(signedIn.Refresh.Token, refreshed.Refresh.Token);
        Assert.False(string.IsNullOrEmpty(refreshed.Response.AccessToken));
        Assert.Equal("Bearer", refreshed.Response.TokenType);
        Assert.Contains(RoleNames.Customer, refreshed.Response.Roles);
        Assert.NotNull(refreshed.Response.WalletNumber);

        var rows = await RowsAsync(userId);
        Assert.Equal(2, rows.Count);
        var (old, next) = (rows[0], rows[1]);
        Assert.Equal(Now(clock), old.RevokedAt);
        Assert.Equal(next.Id, old.ReplacedById);
        Assert.Equal(old.FamilyId, next.FamilyId);
        Assert.Equal(old.SessionStartedAt, next.SessionStartedAt);
        Assert.Equal(Now(clock) + SessionRules.IdleLifetime, next.ExpiresAt);
        Assert.Null(next.RevokedAt);
        Assert.Equal(new RefreshTokenService().Hash(refreshed.Refresh.Token), next.TokenHash);
        Assert.Equal(1, await AuditCountAsync(userId, AuditActions.RefreshRotated));
    }

    [Fact]
    public async Task The_new_token_can_be_used_in_turn_and_the_old_one_can_not_be_the_next_one()
    {
        var (_, email) = await NewUserAsync();
        var clock = NewClock();
        var first = await SignInAsync(email, clock);

        var second = (await RefreshAsync(first.Refresh.Token, clock)).Value!.Refresh!;
        clock.Advance(TimeSpan.FromMinutes(5));
        var third = await RefreshAsync(second.Token, clock);

        Assert.True(third.Succeeded);
        Assert.NotEqual(second.Token, third.Value!.Refresh!.Token);
    }

    [Fact]
    public async Task Using_a_session_adds_seven_days_each_time_but_never_goes_past_thirty_from_the_sign_in()
    {
        var (userId, email) = await NewUserAsync();
        var clock = NewClock();
        var started = Now(clock);
        var token = (await SignInAsync(email, clock)).Refresh.Token;

        foreach (var day in new[] { 6, 12, 18, 24, 29 })
        {
            clock.SetUtcNow(new DateTimeOffset(started.AddDays(day), TimeSpan.Zero));
            var refreshed = (await RefreshAsync(token, clock)).Value!.Refresh!;
            token = refreshed.Token;
            Assert.Equal(DateTime.Compare(started.AddDays(day + 7), started.AddDays(30)) < 0 ? started.AddDays(day + 7) : started.AddDays(30), refreshed.ExpiresAt);
        }

        clock.SetUtcNow(new DateTimeOffset(started.AddDays(30).AddSeconds(1), TimeSpan.Zero));
        var late = await RefreshAsync(token, clock);

        Assert.Equal(ErrorCodes.InvalidRefreshToken, late.ErrorCode);
        Assert.Equal(started.AddDays(30), (await RowsAsync(userId)).Max(row => row.ExpiresAt));
    }

    [Fact]
    public async Task A_token_that_is_not_used_for_seven_days_stops_working()
    {
        var (_, email) = await NewUserAsync();
        var clock = NewClock();
        var signedIn = await SignInAsync(email, clock);
        clock.Advance(SessionRules.IdleLifetime + TimeSpan.FromSeconds(1));

        var result = await RefreshAsync(signedIn.Refresh.Token, clock);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.InvalidRefreshToken, result.ErrorCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-token")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmno+")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnop-")]
    public async Task A_missing_malformed_or_unknown_token_is_refused_with_the_one_code(string? token)
    {
        var result = await RefreshAsync(token, NewClock());

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.InvalidRefreshToken, result.ErrorCode);
    }

    // ---- reuse

    [Fact]
    public async Task A_replaced_token_that_comes_back_after_the_grace_ends_the_whole_session()
    {
        var (userId, email) = await NewUserAsync();
        var clock = NewClock();
        var stolen = (await SignInAsync(email, clock)).Refresh.Token;
        var current = (await RefreshAsync(stolen, clock)).Value!.Refresh!.Token;
        clock.Advance(SessionRules.ReuseGrace + TimeSpan.FromSeconds(1));

        var replay = await RefreshAsync(stolen, clock);
        var afterwards = await RefreshAsync(current, clock);

        Assert.Equal(ErrorCodes.InvalidRefreshToken, replay.ErrorCode);
        Assert.Equal(ErrorCodes.InvalidRefreshToken, afterwards.ErrorCode);
        Assert.All(await RowsAsync(userId), row => Assert.NotNull(row.RevokedAt));
        Assert.Equal(1, await AuditCountAsync(userId, AuditActions.RefreshReuseDetected));
    }

    [Fact]
    public async Task Trying_the_replaced_token_again_adds_no_second_alarm()
    {
        var (userId, email) = await NewUserAsync();
        var clock = NewClock();
        var stolen = (await SignInAsync(email, clock)).Refresh.Token;
        await RefreshAsync(stolen, clock);
        clock.Advance(SessionRules.ReuseGrace + TimeSpan.FromSeconds(1));

        await RefreshAsync(stolen, clock);
        await RefreshAsync(stolen, clock);

        Assert.Equal(1, await AuditCountAsync(userId, AuditActions.RefreshReuseDetected));
    }

    [Fact]
    public async Task A_replaced_token_inside_the_grace_gets_an_access_token_and_no_new_refresh_token()
    {
        var (userId, email) = await NewUserAsync();
        var clock = NewClock();
        var first = (await SignInAsync(email, clock)).Refresh.Token;
        var second = (await RefreshAsync(first, clock)).Value!.Refresh!.Token;
        clock.Advance(TimeSpan.FromSeconds(3));

        var crossing = await RefreshAsync(first, clock);

        Assert.True(crossing.Succeeded);
        Assert.Null(crossing.Value!.Refresh);
        Assert.False(string.IsNullOrEmpty(crossing.Value.Response.AccessToken));
        Assert.Equal(2, (await RowsAsync(userId)).Count);
        Assert.True((await RefreshAsync(second, clock)).Succeeded);
        Assert.Equal(0, await AuditCountAsync(userId, AuditActions.RefreshReuseDetected));
        Assert.Equal(1, await AuditCountAsync(userId, AuditActions.RefreshGraceUsed));
    }

    [Fact]
    public async Task The_grace_covers_one_replacement_so_a_token_two_replacements_back_ends_the_session()
    {
        var (userId, email) = await NewUserAsync();
        var clock = NewClock();
        var first = (await SignInAsync(email, clock)).Refresh.Token;
        var second = (await RefreshAsync(first, clock)).Value!.Refresh!.Token;
        clock.Advance(TimeSpan.FromSeconds(2));
        var third = (await RefreshAsync(second, clock)).Value!.Refresh!.Token;
        clock.Advance(TimeSpan.FromSeconds(2));

        var lateFirst = await RefreshAsync(first, clock);

        Assert.Equal(ErrorCodes.InvalidRefreshToken, lateFirst.ErrorCode);
        Assert.Equal(ErrorCodes.InvalidRefreshToken, (await RefreshAsync(third, clock)).ErrorCode);
        Assert.Equal(1, await AuditCountAsync(userId, AuditActions.RefreshReuseDetected));
    }

    [Fact]
    public async Task A_grace_use_is_on_record_with_the_address_it_came_from()
    {
        var (userId, email) = await NewUserAsync();
        var clock = NewClock();
        var first = (await SignInAsync(email, clock)).Refresh.Token;
        await RefreshAsync(first, clock);
        clock.Advance(TimeSpan.FromSeconds(1));

        await RefreshAsync(first, clock);

        await using var db = sql.NewContext();
        var entry = await db.AuditLogs.AsNoTracking().SingleAsync(log => log.ActorUserId == userId && log.Action == AuditActions.RefreshGraceUsed, TestContext.Current.CancellationToken);
        Assert.Equal(AuditEntityTypes.Session, entry.EntityType);
        Assert.Equal("203.0.113.70", entry.IpAddress);
    }

    [Fact]
    public async Task The_keys_of_the_token_rows_are_made_when_they_are_added_and_the_replacement_points_at_the_new_one()
    {
        var (userId, email) = await NewUserAsync();
        var clock = NewClock();
        var first = (await SignInAsync(email, clock)).Refresh.Token;
        await RefreshAsync(first, clock);

        var rows = await RowsAsync(userId);

        Assert.NotEqual(Guid.Empty, rows[0].Id);
        Assert.NotEqual(rows[0].Id, rows[1].Id);
        Assert.Equal(rows[1].Id, rows[0].ReplacedById);
    }

    [Fact]
    public async Task A_reuse_ends_only_the_session_it_happened_in()
    {
        var (_, email) = await NewUserAsync();
        var clock = NewClock();
        var attacked = (await SignInAsync(email, clock)).Refresh.Token;
        var untouched = (await SignInAsync(email, clock, OtherDevice)).Refresh.Token;
        await RefreshAsync(attacked, clock);
        clock.Advance(SessionRules.ReuseGrace + TimeSpan.FromSeconds(1));

        await RefreshAsync(attacked, clock);

        Assert.True((await RefreshAsync(untouched, clock)).Succeeded);
    }

    [Fact]
    public async Task Parallel_refreshes_with_one_token_rotate_it_once_and_never_fork_the_session()
    {
        var (userId, email) = await NewUserAsync();
        var signedIn = await SignInAsync(email, TimeProvider.System);

        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => RefreshAsync(signedIn.Refresh.Token, TimeProvider.System)));

        Assert.All(results, result => Assert.True(result.Succeeded));
        Assert.Equal(1, results.Count(result => result.Value!.Refresh is not null));
        var rows = await RowsAsync(userId);
        Assert.Equal(2, rows.Count);
        Assert.Single(rows, row => row.RevokedAt is null);
        Assert.Equal(0, await AuditCountAsync(userId, AuditActions.RefreshReuseDetected));
    }

    // Holds the named lock of a session in another transaction, starts the call, and checks that the call is still
    // waiting a moment later. Then lets go, and the call finishes. That is what makes two requests take turns.
    private async Task<T> RunWhileLockedAsync<T>(Guid familyId, Func<Task<T>> start)
    {
        await using var holder = sql.NewContext();
        Task<T> call;
        await using (var transaction = await holder.Database.BeginTransactionAsync(TestContext.Current.CancellationToken))
        {
            await holder.LockResourceAsync("refresh-family:" + familyId, TestContext.Current.CancellationToken);
            call = start();
            await Task.Delay(TimeSpan.FromSeconds(1.5), TestContext.Current.CancellationToken);
            Assert.False(call.IsCompleted, "The call did not wait for the lock of its session.");
        }

        return await call;
    }

    [Fact]
    public async Task A_refresh_waits_for_the_lock_of_its_session()
    {
        var (userId, email) = await NewUserAsync();
        var clock = NewClock();
        var token = (await SignInAsync(email, clock)).Refresh.Token;
        var familyId = (await RowsAsync(userId)).Single().FamilyId;

        var result = await RunWhileLockedAsync(familyId, () => RefreshAsync(token, clock));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Signing_out_waits_for_the_lock_of_its_session()
    {
        var (userId, email) = await NewUserAsync();
        var clock = NewClock();
        var token = (await SignInAsync(email, clock)).Refresh.Token;
        var familyId = (await RowsAsync(userId)).Single().FamilyId;

        await RunWhileLockedAsync(familyId, async () =>
        {
            await EndAsync(token, clock);
            return 0;
        });

        Assert.Equal(1, await AuditCountAsync(userId, AuditActions.Logout));
    }

    [Fact]
    public async Task Ending_a_session_from_the_list_waits_for_its_lock()
    {
        var (userId, email) = await NewUserAsync();
        var clock = NewClock();
        await SignInAsync(email, clock);
        var familyId = (await RowsAsync(userId)).Single().FamilyId;

        var result = await RunWhileLockedAsync(familyId, () => RevokeAsync(userId, familyId, null, clock));

        Assert.True(result.Succeeded);
    }

    // ---- ending

    [Fact]
    public async Task Signing_out_ends_the_session_and_its_token_stops_working()
    {
        var (userId, email) = await NewUserAsync();
        var clock = NewClock();
        var token = (await SignInAsync(email, clock)).Refresh.Token;

        await EndAsync(token, clock);

        Assert.Equal(ErrorCodes.InvalidRefreshToken, (await RefreshAsync(token, clock)).ErrorCode);
        Assert.All(await RowsAsync(userId), row => Assert.NotNull(row.RevokedAt));
        Assert.Equal(1, await AuditCountAsync(userId, AuditActions.Logout));
    }

    [Fact]
    public async Task Signing_out_twice_or_with_nothing_is_not_an_error_and_adds_no_entry()
    {
        var (userId, email) = await NewUserAsync();
        var clock = NewClock();
        var token = (await SignInAsync(email, clock)).Refresh.Token;

        await EndAsync(token, clock);
        await EndAsync(token, clock);
        await EndAsync(null, clock);
        await EndAsync("not-a-token", clock);

        Assert.Equal(1, await AuditCountAsync(userId, AuditActions.Logout));
    }

    [Fact]
    public async Task Signing_out_does_not_end_the_other_sessions_of_the_user()
    {
        var (_, email) = await NewUserAsync();
        var clock = NewClock();
        var here = (await SignInAsync(email, clock)).Refresh.Token;
        var there = (await SignInAsync(email, clock, OtherDevice)).Refresh.Token;

        await EndAsync(here, clock);

        Assert.True((await RefreshAsync(there, clock)).Succeeded);
    }

    [Fact]
    public async Task A_token_after_the_replacement_signed_out_is_just_refused_with_no_alarm()
    {
        var (userId, email) = await NewUserAsync();
        var clock = NewClock();
        var first = (await SignInAsync(email, clock)).Refresh.Token;
        var second = (await RefreshAsync(first, clock)).Value!.Refresh!.Token;
        await EndAsync(second, clock);
        clock.Advance(SessionRules.ReuseGrace + TimeSpan.FromSeconds(1));

        var result = await RefreshAsync(first, clock);

        Assert.Equal(ErrorCodes.InvalidRefreshToken, result.ErrorCode);
        Assert.Equal(0, await AuditCountAsync(userId, AuditActions.RefreshReuseDetected));
    }

    // ---- the list

    [Fact]
    public async Task The_list_has_the_live_sessions_newest_first_and_marks_the_one_of_this_browser()
    {
        var (userId, email) = await NewUserAsync();
        var clock = NewClock();
        var older = (await SignInAsync(email, clock)).Refresh.Token;
        clock.Advance(TimeSpan.FromHours(1));
        await SignInAsync(email, clock, OtherDevice);

        var list = await ListAsync(userId, older, clock);

        Assert.Equal(2, list.Count);
        Assert.Equal("Safari on iPhone", list[0].UserAgent);
        Assert.Equal("198.51.100.9", list[0].IpAddress);
        Assert.False(list[0].Current);
        Assert.Equal("Chrome on macOS", list[1].UserAgent);
        Assert.True(list[1].Current);
    }

    [Fact]
    public async Task A_session_keeps_its_sign_in_time_and_shows_when_it_was_last_used()
    {
        var (userId, email) = await NewUserAsync();
        var clock = NewClock();
        var started = Now(clock);
        var token = (await SignInAsync(email, clock)).Refresh.Token;
        clock.Advance(TimeSpan.FromDays(2));
        var used = Now(clock);
        await RefreshAsync(token, clock);

        var session = Assert.Single(await ListAsync(userId, null, clock));

        Assert.Equal(started, session.SignedInAt);
        Assert.Equal(used, session.LastActiveAt);
        Assert.False(session.Current);
    }

    [Fact]
    public async Task The_list_leaves_out_ended_and_expired_sessions()
    {
        var (userId, email) = await NewUserAsync();
        var clock = NewClock();
        var ended = (await SignInAsync(email, clock)).Refresh.Token;
        await EndAsync(ended, clock);
        await SignInAsync(email, clock, OtherDevice);

        Assert.Single(await ListAsync(userId, null, clock));

        clock.Advance(SessionRules.IdleLifetime + TimeSpan.FromSeconds(1));
        Assert.Empty(await ListAsync(userId, null, clock));
    }

    [Fact]
    public async Task A_cookie_of_another_user_does_not_mark_anything_as_current()
    {
        var (userId, email) = await NewUserAsync();
        var (_, otherEmail) = await NewUserAsync();
        var clock = NewClock();
        await SignInAsync(email, clock);
        var strangers = (await SignInAsync(otherEmail, clock)).Refresh.Token;

        var list = await ListAsync(userId, strangers, clock);

        Assert.All(list, session => Assert.False(session.Current));
    }

    // ---- revoking one

    [Fact]
    public async Task A_user_can_end_another_of_their_sessions_and_the_current_one_is_reported_as_such()
    {
        var (userId, email) = await NewUserAsync();
        var clock = NewClock();
        var here = (await SignInAsync(email, clock)).Refresh.Token;
        var there = (await SignInAsync(email, clock, OtherDevice)).Refresh.Token;
        var list = await ListAsync(userId, here, clock);
        var other = list.Single(session => !session.Current);
        var current = list.Single(session => session.Current);

        var endOther = await RevokeAsync(userId, other.Id, here, clock);
        var endCurrent = await RevokeAsync(userId, current.Id, here, clock);

        Assert.True(endOther.Succeeded);
        Assert.False(endOther.Value);
        Assert.True(endCurrent.Succeeded);
        Assert.True(endCurrent.Value);
        Assert.Equal(ErrorCodes.InvalidRefreshToken, (await RefreshAsync(there, clock)).ErrorCode);
        Assert.Equal(ErrorCodes.InvalidRefreshToken, (await RefreshAsync(here, clock)).ErrorCode);
        Assert.Equal(2, await AuditCountAsync(userId, AuditActions.SessionRevoked));
    }

    [Fact]
    public async Task A_session_of_someone_else_is_the_same_as_one_that_does_not_exist()
    {
        var (_, email) = await NewUserAsync();
        var (intruderId, _) = await NewUserAsync();
        var clock = NewClock();
        var token = (await SignInAsync(email, clock)).Refresh.Token;
        var victimSession = (await RowsAsyncByToken(token)).FamilyId;

        var attempt = await RevokeAsync(intruderId, victimSession, null, clock);
        var unknown = await RevokeAsync(intruderId, Guid.NewGuid(), null, clock);

        Assert.Equal(ErrorCodes.SessionNotFound, attempt.ErrorCode);
        Assert.Equal(ErrorCodes.SessionNotFound, unknown.ErrorCode);
        Assert.True((await RefreshAsync(token, clock)).Succeeded);
    }

    [Fact]
    public async Task A_session_that_is_already_ended_is_not_found()
    {
        var (userId, email) = await NewUserAsync();
        var clock = NewClock();
        await SignInAsync(email, clock);
        var id = (await ListAsync(userId, null, clock)).Single().Id;
        await RevokeAsync(userId, id, null, clock);

        var again = await RevokeAsync(userId, id, null, clock);

        Assert.Equal(ErrorCodes.SessionNotFound, again.ErrorCode);
        Assert.Equal(1, await AuditCountAsync(userId, AuditActions.SessionRevoked));
    }

    private async Task<RefreshToken> RowsAsyncByToken(string token)
    {
        await using var db = sql.NewContext();
        var hash = new RefreshTokenService().Hash(token);
        return await db.RefreshTokens.AsNoTracking().SingleAsync(row => row.TokenHash == hash);
    }
}
