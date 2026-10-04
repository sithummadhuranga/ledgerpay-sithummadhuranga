using LedgerPay.Application.Auth;
using LedgerPay.Application.Common;
using LedgerPay.Domain.Constants;
using LedgerPay.Domain.Enums;
using LedgerPay.Infrastructure.Persistence;
using LedgerPay.Infrastructure.Security;
using LedgerPay.Infrastructure.Seeding;
using LedgerPay.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace LedgerPay.IntegrationTests.Auth;

public class AuthServiceTests(SqlServerFixture sql)
{
    private const string Password = "Kandy-Lake-2026!";

    private static readonly RequestInfo Caller = new("203.0.113.60", "corr-auth-test");

    private static long nextPhone = 40_000_000;

    private static string NewEmail() => $"user.{Guid.NewGuid():N}@example.com";

    private static string NewPhone() => "+947" + Interlocked.Increment(ref nextPhone);

    private static RegisterRequest NewRegistration(string? email = null, string? phone = null, string name = "Nimali Perera") =>
        new(name, email ?? NewEmail(), phone ?? NewPhone(), Password);

    private async Task<ServiceResult<RegisterResponse>> RegisterAsync(RegisterRequest request, TimeProvider? clock = null)
    {
        await using var db = sql.NewContext();
        return await TestServices.Auth(db, clock).RegisterAsync(request, Caller, CancellationToken.None);
    }

    private async Task<ServiceResult<LoginResponse>> LoginAsync(string email, string password, TimeProvider? clock = null)
    {
        await using var db = sql.NewContext();
        return await TestServices.Auth(db, clock).LoginAsync(new LoginRequest(email, password), Caller, CancellationToken.None);
    }

    private async Task<(string Email, FakeTimeProvider Clock)> RegisteredUserAsync()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 4, 9, 0, 0, TimeSpan.Zero));
        var email = NewEmail();
        var registered = await RegisterAsync(NewRegistration(email), clock);
        Assert.True(registered.Succeeded);
        return (email, clock);
    }

    private async Task<Domain.Entities.User> StoredUserAsync(string email)
    {
        await using var check = sql.NewContext();
        return await check.Users.AsNoTracking().SingleAsync(user => user.Email == email, TestContext.Current.CancellationToken);
    }

    // ---- register

    [Fact]
    public async Task Registering_creates_a_customer_with_an_empty_active_wallet()
    {
        var email = NewEmail();
        var phone = NewPhone();

        var result = await RegisterAsync(new RegisterRequest("Nimali Perera", email, phone, Password));

        Assert.True(result.Succeeded);
        Assert.Equal(("Nimali Perera", email, phone), (result.Value!.FullName, result.Value.Email, result.Value.Phone));
        Assert.Matches("^[1-9][0-9]{11}$", result.Value.WalletNumber);
        await using var check = sql.NewContext();
        var user = await check.Users.AsNoTracking().Include(u => u.Wallet).Include(u => u.UserRoles).ThenInclude(r => r.Role)
            .SingleAsync(u => u.Email == email, TestContext.Current.CancellationToken);
        Assert.Equal([RoleNames.Customer], user.UserRoles.Select(r => r.Role.Name));
        Assert.Equal(result.Value.WalletNumber, user.Wallet!.WalletNumber);
        Assert.Equal(0m, user.Wallet.Balance);
        Assert.Equal(WalletStatus.Active, user.Wallet.Status);
        var account = await check.LedgerAccounts.AsNoTracking().SingleAsync(a => a.WalletId == user.Wallet.Id, TestContext.Current.CancellationToken);
        Assert.Equal(LedgerAccountType.Liability, account.Type);
    }

    [Fact]
    public async Task The_password_is_stored_as_a_hash_that_still_verifies()
    {
        var email = NewEmail();
        await RegisterAsync(NewRegistration(email));

        var user = await StoredUserAsync(email);

        Assert.DoesNotContain(Password, user.PasswordHash);
        Assert.True(new PasswordService().Verify(user.PasswordHash, Password));
        Assert.False(new PasswordService().Verify(user.PasswordHash, Password + "x"));
    }

    [Fact]
    public async Task The_email_is_stored_in_lower_case_and_the_name_is_trimmed()
    {
        var local = "Mixed." + Guid.NewGuid().ToString("N");

        var result = await RegisterAsync(NewRegistration(email: local + "@Example.COM", name: "  Kasun Jayawardena  "));

        Assert.True(result.Succeeded);
        Assert.Equal((local + "@example.com").ToLowerInvariant(), result.Value!.Email);
        Assert.Equal("Kasun Jayawardena", result.Value.FullName);
        Assert.NotNull(await StoredUserAsync(result.Value.Email));
    }

    [Fact]
    public async Task Registering_writes_an_audit_entry_for_the_new_user()
    {
        var email = NewEmail();

        var result = await RegisterAsync(NewRegistration(email));

        var user = await StoredUserAsync(email);
        await using var check = sql.NewContext();
        var audit = await check.AuditLogs.AsNoTracking().SingleAsync(
            log => log.ActorUserId == user.Id && log.Action == AuditActions.Register, TestContext.Current.CancellationToken);
        Assert.Equal(AuditEntityTypes.User, audit.EntityType);
        Assert.Equal(result.Value!.WalletNumber, audit.EntityReference);
        Assert.Equal("203.0.113.60", audit.IpAddress);
        Assert.Equal("corr-auth-test", audit.CorrelationId);
    }

    [Fact]
    public async Task A_second_registration_with_the_same_email_in_any_letter_case_is_a_conflict()
    {
        var email = NewEmail();
        await RegisterAsync(NewRegistration(email));

        var second = await RegisterAsync(NewRegistration(email.ToUpperInvariant()));

        Assert.Equal(ErrorCodes.EmailAlreadyRegistered, second.ErrorCode);
        await using var check = sql.NewContext();
        Assert.Equal(1, await check.Users.CountAsync(u => u.Email == email, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_second_registration_with_the_same_phone_is_a_conflict_and_creates_nothing()
    {
        var phone = NewPhone();
        await RegisterAsync(NewRegistration(phone: phone));
        var email = NewEmail();

        var second = await RegisterAsync(NewRegistration(email, phone));

        Assert.Equal(ErrorCodes.PhoneAlreadyRegistered, second.ErrorCode);
        await using var check = sql.NewContext();
        Assert.False(await check.Users.AnyAsync(u => u.Email == email, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task When_both_the_email_and_the_phone_are_taken_the_email_is_reported()
    {
        var email = NewEmail();
        var phone = NewPhone();
        await RegisterAsync(NewRegistration(email, phone));

        var second = await RegisterAsync(NewRegistration(email, phone));

        Assert.Equal(ErrorCodes.EmailAlreadyRegistered, second.ErrorCode);
    }

    [Fact]
    public async Task Eight_parallel_registrations_with_one_email_create_one_user()
    {
        var email = NewEmail();

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => RegisterAsync(NewRegistration(email))));

        Assert.Equal(1, results.Count(result => result.Succeeded));
        Assert.All(results.Where(result => !result.Succeeded), result => Assert.Equal(ErrorCodes.EmailAlreadyRegistered, result.ErrorCode));
        await using var check = sql.NewContext();
        Assert.Equal(1, await check.Users.CountAsync(u => u.Email == email, TestContext.Current.CancellationToken));
        Assert.Equal(1, await check.Wallets.CountAsync(w => w.User.Email == email, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Eight_parallel_registrations_with_one_phone_create_one_user()
    {
        var phone = NewPhone();

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => RegisterAsync(NewRegistration(phone: phone))));

        Assert.Equal(1, results.Count(result => result.Succeeded));
        Assert.All(results.Where(result => !result.Succeeded), result => Assert.Equal(ErrorCodes.PhoneAlreadyRegistered, result.ErrorCode));
    }

    [Fact]
    public async Task Twenty_parallel_registrations_all_get_different_wallet_numbers()
    {
        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => RegisterAsync(NewRegistration())));

        Assert.All(results, result => Assert.True(result.Succeeded));
        Assert.Equal(20, results.Select(result => result.Value!.WalletNumber).Distinct().Count());
    }

    [Fact]
    public async Task A_new_customer_can_sign_in_straight_away()
    {
        var email = NewEmail();
        await RegisterAsync(NewRegistration(email));

        var login = await LoginAsync(email, Password);

        Assert.True(login.Succeeded);
        Assert.Equal([RoleNames.Customer], login.Value!.Roles);
        Assert.NotNull(login.Value.WalletNumber);
    }

    // ---- login

    [Fact]
    public async Task A_seeded_customer_signs_in_with_the_seed_password()
    {
        var login = await LoginAsync(SeedData.Customers[0].Email, "Customer-pass-for-tests-3!");

        Assert.True(login.Succeeded);
        Assert.Equal("Bearer", login.Value!.TokenType);
        Assert.Equal([RoleNames.Customer], login.Value.Roles);
        Assert.Equal(SeedData.Customers[0].FullName, login.Value.FullName);
        Assert.Matches("^[0-9]{12}$", login.Value.WalletNumber);
        Assert.False(string.IsNullOrWhiteSpace(login.Value.AccessToken));
    }

    [Fact]
    public async Task The_back_office_users_sign_in_with_their_roles_and_no_wallet()
    {
        var operatorLogin = await LoginAsync(SeedData.OperatorEmail, "Operator-pass-for-tests-2!");
        var adminLogin = await LoginAsync(SeedData.AdminEmail, "Admin-pass-for-tests-1!");

        Assert.Equal([RoleNames.Operator], operatorLogin.Value!.Roles);
        Assert.Equal([RoleNames.Admin], adminLogin.Value!.Roles);
        Assert.Null(operatorLogin.Value.WalletNumber);
        Assert.Null(adminLogin.Value.WalletNumber);
    }

    [Fact]
    public async Task The_email_is_not_case_sensitive_at_sign_in()
    {
        var login = await LoginAsync(SeedData.Customers[1].Email.ToUpperInvariant(), "Customer-pass-for-tests-3!");

        Assert.True(login.Succeeded);
    }

    [Fact]
    public async Task The_token_lasts_fifteen_minutes_from_the_sign_in_time()
    {
        var (email, clock) = await RegisteredUserAsync();

        var login = await LoginAsync(email, Password, clock);

        Assert.Equal(clock.GetUtcNow().UtcDateTime.AddMinutes(15), login.Value!.ExpiresAt);
    }

    [Fact]
    public async Task A_wrong_password_and_an_unknown_email_get_the_same_answer()
    {
        var (email, _) = await RegisteredUserAsync();

        var wrongPassword = await LoginAsync(email, "Wrong-Password-1!");
        var unknownEmail = await LoginAsync(NewEmail(), "Wrong-Password-1!");

        Assert.Equal(ErrorCodes.InvalidCredentials, wrongPassword.ErrorCode);
        Assert.Equal(ErrorCodes.InvalidCredentials, unknownEmail.ErrorCode);
        Assert.Null(wrongPassword.RetryAfterSeconds);
        Assert.Null(unknownEmail.RetryAfterSeconds);
    }

    [Fact]
    public async Task A_failed_sign_in_is_counted_and_audited()
    {
        var (email, clock) = await RegisteredUserAsync();

        await LoginAsync(email, "Wrong-Password-1!", clock);

        var user = await StoredUserAsync(email);
        Assert.Equal(1, user.FailedLoginCount);
        Assert.Null(user.LockoutEnd);
        await using var check = sql.NewContext();
        Assert.Equal(1, await check.AuditLogs.CountAsync(
            log => log.ActorUserId == user.Id && log.Action == AuditActions.LoginFailed, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_successful_sign_in_is_audited_and_clears_earlier_failures()
    {
        var (email, clock) = await RegisteredUserAsync();
        for (var i = 0; i < 3; i++)
        {
            await LoginAsync(email, "Wrong-Password-1!", clock);
        }

        var login = await LoginAsync(email, Password, clock);

        Assert.True(login.Succeeded);
        var user = await StoredUserAsync(email);
        Assert.Equal(0, user.FailedLoginCount);
        await using var check = sql.NewContext();
        Assert.Equal(1, await check.AuditLogs.CountAsync(
            log => log.ActorUserId == user.Id && log.Action == AuditActions.LoginSucceeded, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Failures_followed_by_a_success_do_not_add_up_to_a_lock()
    {
        var (email, clock) = await RegisteredUserAsync();
        for (var i = 0; i < 4; i++)
        {
            await LoginAsync(email, "Wrong-Password-1!", clock);
        }

        await LoginAsync(email, Password, clock);
        for (var i = 0; i < 4; i++)
        {
            await LoginAsync(email, "Wrong-Password-1!", clock);
        }

        var login = await LoginAsync(email, Password, clock);
        Assert.True(login.Succeeded);
    }

    // ---- lockout

    [Fact]
    public async Task The_fifth_failed_sign_in_locks_the_account_for_fifteen_minutes()
    {
        var (email, clock) = await RegisteredUserAsync();
        for (var i = 0; i < 4; i++)
        {
            var failed = await LoginAsync(email, "Wrong-Password-1!", clock);
            Assert.Equal(ErrorCodes.InvalidCredentials, failed.ErrorCode);
        }

        var fifth = await LoginAsync(email, "Wrong-Password-1!", clock);

        Assert.Equal(ErrorCodes.AccountLocked, fifth.ErrorCode);
        Assert.Equal(15 * 60, fifth.RetryAfterSeconds);
        var user = await StoredUserAsync(email);
        Assert.Equal(clock.GetUtcNow().UtcDateTime.AddMinutes(15), user.LockoutEnd);
        await using var check = sql.NewContext();
        Assert.Equal(1, await check.AuditLogs.CountAsync(
            log => log.ActorUserId == user.Id && log.Action == AuditActions.AccountLocked, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task The_right_password_is_refused_while_the_account_is_locked()
    {
        var (email, clock) = await RegisteredUserAsync();
        for (var i = 0; i < 5; i++)
        {
            await LoginAsync(email, "Wrong-Password-1!", clock);
        }
        clock.Advance(TimeSpan.FromMinutes(5));

        var login = await LoginAsync(email, Password, clock);

        Assert.Equal(ErrorCodes.AccountLocked, login.ErrorCode);
        Assert.Equal(10 * 60, login.RetryAfterSeconds);
    }

    [Fact]
    public async Task During_the_lock_the_right_and_the_wrong_password_get_exactly_the_same_answer()
    {
        var (email, clock) = await RegisteredUserAsync();
        for (var i = 0; i < 5; i++)
        {
            await LoginAsync(email, "Wrong-Password-1!", clock);
        }
        clock.Advance(TimeSpan.FromMinutes(2));

        var right = await LoginAsync(email, Password, clock);
        var wrong = await LoginAsync(email, "Wrong-Password-1!", clock);

        Assert.Equal(ErrorCodes.AccountLocked, right.ErrorCode);
        Assert.Equal(ErrorCodes.AccountLocked, wrong.ErrorCode);
        Assert.Equal(right.RetryAfterSeconds, wrong.RetryAfterSeconds);
    }

    [Fact]
    public async Task Attempts_during_the_lock_do_not_extend_it()
    {
        var (email, clock) = await RegisteredUserAsync();
        for (var i = 0; i < 5; i++)
        {
            await LoginAsync(email, "Wrong-Password-1!", clock);
        }
        var lockedUntil = (await StoredUserAsync(email)).LockoutEnd;
        clock.Advance(TimeSpan.FromMinutes(3));

        await LoginAsync(email, "Wrong-Password-1!", clock);
        await LoginAsync(email, Password, clock);

        Assert.Equal(lockedUntil, (await StoredUserAsync(email)).LockoutEnd);
    }

    [Fact]
    public async Task After_fifteen_minutes_the_right_password_works_and_the_count_starts_again()
    {
        var (email, clock) = await RegisteredUserAsync();
        for (var i = 0; i < 5; i++)
        {
            await LoginAsync(email, "Wrong-Password-1!", clock);
        }
        clock.Advance(TimeSpan.FromMinutes(15));

        var login = await LoginAsync(email, Password, clock);

        Assert.True(login.Succeeded);
        var user = await StoredUserAsync(email);
        Assert.Equal(0, user.FailedLoginCount);
        Assert.Null(user.LockoutEnd);
    }

    [Fact]
    public async Task After_the_lock_ends_a_wrong_password_counts_as_the_first_failure_again()
    {
        var (email, clock) = await RegisteredUserAsync();
        for (var i = 0; i < 5; i++)
        {
            await LoginAsync(email, "Wrong-Password-1!", clock);
        }
        clock.Advance(TimeSpan.FromMinutes(15));

        var wrong = await LoginAsync(email, "Wrong-Password-1!", clock);

        Assert.Equal(ErrorCodes.InvalidCredentials, wrong.ErrorCode);
        var user = await StoredUserAsync(email);
        Assert.Equal(1, user.FailedLoginCount);
        Assert.Null(user.LockoutEnd);
    }

    [Fact]
    public async Task Twenty_parallel_wrong_passwords_cannot_get_more_than_five_guesses_in()
    {
        var (email, clock) = await RegisteredUserAsync();

        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => LoginAsync(email, "Wrong-Password-1!", clock)));

        Assert.Equal(4, results.Count(result => result.ErrorCode == ErrorCodes.InvalidCredentials));
        Assert.Equal(16, results.Count(result => result.ErrorCode == ErrorCodes.AccountLocked));
        Assert.Equal(5, (await StoredUserAsync(email)).FailedLoginCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Kandy-Lake-2026!")]
    [InlineData("not-the-password")]
    public void Checking_an_unknown_account_never_succeeds(string password)
    {
        Assert.False(new PasswordService().VerifyUnknownAccount(password));
    }

    [Fact]
    public async Task An_unknown_email_does_one_hash_check_and_a_known_email_does_one_too()
    {
        // The same amount of work either way, so the response time does not give the email away.
        var (email, _) = await RegisteredUserAsync();
        var known = new CountingPasswordService();
        var unknown = new CountingPasswordService();

        await using (var db = sql.NewContext())
        {
            await TestServices.Auth(db, passwords: known).LoginAsync(new LoginRequest(email, "Wrong-Password-1!"), Caller, CancellationToken.None);
        }

        await using (var db = sql.NewContext())
        {
            await TestServices.Auth(db, passwords: unknown).LoginAsync(new LoginRequest(NewEmail(), "Wrong-Password-1!"), Caller, CancellationToken.None);
        }

        Assert.Equal((1, 0, 0), (known.Verifies, known.UnknownAccountChecks, known.Hashes));
        Assert.Equal((0, 1, 0), (unknown.Verifies, unknown.UnknownAccountChecks, unknown.Hashes));
    }

    [Fact]
    public async Task An_unknown_email_never_gets_locked_and_always_gets_the_same_answer()
    {
        var unknown = NewEmail();

        var results = new List<ServiceResult<LoginResponse>>();
        for (var i = 0; i < 7; i++)
        {
            results.Add(await LoginAsync(unknown, "Wrong-Password-1!"));
        }

        Assert.All(results, result => Assert.Equal(ErrorCodes.InvalidCredentials, result.ErrorCode));
    }

    [Fact]
    public async Task An_unknown_email_is_audited_without_an_actor_and_without_the_email()
    {
        var unknown = NewEmail();

        await LoginAsync(unknown, "Wrong-Password-1!");

        await using var check = sql.NewContext();
        var rows = await check.AuditLogs.AsNoTracking()
            .Where(log => log.Action == AuditActions.LoginFailed && log.ActorUserId == null && log.Details == "Unknown account")
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.NotEmpty(rows);
        Assert.DoesNotContain(rows, row => (row.Details + row.EntityReference + row.IpAddress + row.CorrelationId).Contains(unknown));
    }

    [Fact]
    public async Task No_audit_entry_of_a_user_holds_the_email_or_a_password()
    {
        var (email, clock) = await RegisteredUserAsync();
        await LoginAsync(email, "Wrong-Password-1!", clock);
        await LoginAsync(email, Password, clock);
        for (var i = 0; i < 4; i++)
        {
            await LoginAsync(email, "Wrong-Password-1!", clock);
        }

        var user = await StoredUserAsync(email);
        await using var check = sql.NewContext();
        var rows = await check.AuditLogs.AsNoTracking().Where(log => log.ActorUserId == user.Id).ToListAsync(TestContext.Current.CancellationToken);
        Assert.True(rows.Count >= 6);
        foreach (var row in rows)
        {
            var text = row.Action + row.EntityType + row.EntityReference + row.Details + row.IpAddress + row.CorrelationId;
            Assert.DoesNotContain(email, text);
            Assert.DoesNotContain(user.Phone, text);
            Assert.DoesNotContain("Wrong-Password-1!", text);
            Assert.DoesNotContain(Password, text);
        }
    }
}
