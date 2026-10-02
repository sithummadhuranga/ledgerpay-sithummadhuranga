using LedgerPay.Domain.Constants;
using LedgerPay.Domain.Enums;
using LedgerPay.Infrastructure.Security;
using LedgerPay.Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;

namespace LedgerPay.IntegrationTests.Seeding;

public class SeederTests(SqlServerFixture sql)
{
    private static readonly SeedOptions Passwords = new()
    {
        AdminPassword = "Admin-pass-for-tests-1!",
        OperatorPassword = "Operator-pass-for-tests-2!",
        CustomerPassword = "Customer-pass-for-tests-3!"
    };

    private static bool IsSeeded(string email) =>
        email == SeedData.AdminEmail || email == SeedData.OperatorEmail || SeedData.Customers.Any(customer => customer.Email == email);

    private static Seeder NewSeeder(Infrastructure.Persistence.AppDbContext db) =>
        new(db, new PasswordService(), TimeProvider.System);

    [Fact]
    public async Task Seed_creates_the_three_roles()
    {
        await using var db = sql.NewContext();

        await NewSeeder(db).SeedAsync(Passwords, TestContext.Current.CancellationToken);

        var roles = await db.Roles.Select(role => role.Name).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Contains(RoleNames.Customer, roles);
        Assert.Contains(RoleNames.Operator, roles);
        Assert.Contains(RoleNames.Admin, roles);
    }

    [Fact]
    public async Task Seed_creates_the_system_ledger_accounts()
    {
        await using var db = sql.NewContext();

        await NewSeeder(db).SeedAsync(Passwords, TestContext.Current.CancellationToken);

        var settlement = await db.LedgerAccounts.SingleAsync(
            account => account.Code == LedgerAccountCodes.SettlementFloat, TestContext.Current.CancellationToken);
        var revenue = await db.LedgerAccounts.SingleAsync(
            account => account.Code == LedgerAccountCodes.FeeRevenue, TestContext.Current.CancellationToken);
        Assert.Equal(LedgerAccountType.Asset, settlement.Type);
        Assert.Equal(LedgerAccountType.Revenue, revenue.Type);
        Assert.Null(settlement.WalletId);
        Assert.Null(revenue.WalletId);
    }

    [Fact]
    public async Task Seed_stores_the_limits_from_the_assignment_as_settings()
    {
        await using var db = sql.NewContext();

        await NewSeeder(db).SeedAsync(Passwords, TestContext.Current.CancellationToken);

        var settings = await db.SystemSettings.ToDictionaryAsync(
            setting => setting.Key, setting => setting.Value, TestContext.Current.CancellationToken);
        Assert.Equal(0.50m, settings[SettingKeys.FeePercent]);
        Assert.Equal(10.00m, settings[SettingKeys.FeeMinimum]);
        Assert.Equal(250.00m, settings[SettingKeys.FeeMaximum]);
        Assert.Equal(100.00m, settings[SettingKeys.TransferMinimum]);
        Assert.Equal(500_000.00m, settings[SettingKeys.TransferMaximum]);
        Assert.Equal(2_000_000.00m, settings[SettingKeys.WalletBalanceCap]);
    }

    [Fact]
    public async Task Seed_creates_one_operator_one_admin_and_three_customers()
    {
        await using var db = sql.NewContext();

        await NewSeeder(db).SeedAsync(Passwords, TestContext.Current.CancellationToken);

        var roleByEmail = await db.UserRoles
            .Select(userRole => new { userRole.User.Email, Role = userRole.Role.Name })
            .ToListAsync(TestContext.Current.CancellationToken);
        var seeded = roleByEmail.Where(row => IsSeeded(row.Email)).ToList();
        Assert.Equal(1, seeded.Count(row => row.Role == RoleNames.Admin));
        Assert.Equal(1, seeded.Count(row => row.Role == RoleNames.Operator));
        Assert.Equal(3, seeded.Count(row => row.Role == RoleNames.Customer));
    }

    [Fact]
    public async Task Seeded_customers_have_a_twelve_digit_wallet_with_zero_balance_and_a_ledger_account()
    {
        await using var db = sql.NewContext();

        await NewSeeder(db).SeedAsync(Passwords, TestContext.Current.CancellationToken);

        var customers = await db.Users
            .Include(user => user.Wallet)
            .Where(user => user.Wallet != null)
            .ToListAsync(TestContext.Current.CancellationToken);
        var seeded = customers.Where(user => IsSeeded(user.Email)).ToList();
        Assert.Equal(3, seeded.Count);
        foreach (var customer in seeded)
        {
            Assert.Equal(12, customer.Wallet!.WalletNumber.Length);
            Assert.Equal(0m, customer.Wallet.Balance);
            Assert.Equal(WalletStatus.Active, customer.Wallet.Status);
            var account = await db.LedgerAccounts.SingleAsync(
                account => account.WalletId == customer.Wallet.Id, TestContext.Current.CancellationToken);
            Assert.Equal(LedgerAccountType.Liability, account.Type);
        }
    }

    [Fact]
    public async Task Seeded_passwords_are_hashed_and_still_verify()
    {
        await using var db = sql.NewContext();
        var hasher = new PasswordService();

        await NewSeeder(db).SeedAsync(Passwords, TestContext.Current.CancellationToken);

        var admin = await db.Users.SingleAsync(
            user => user.Email == SeedData.AdminEmail, TestContext.Current.CancellationToken);
        Assert.DoesNotContain(Passwords.AdminPassword, admin.PasswordHash);
        Assert.True(hasher.Verify(admin.PasswordHash, Passwords.AdminPassword));
        Assert.False(hasher.Verify(admin.PasswordHash, Passwords.OperatorPassword));
    }

    [Fact]
    public async Task Seeding_twice_does_not_create_duplicates()
    {
        await using var db = sql.NewContext();
        await NewSeeder(db).SeedAsync(Passwords, TestContext.Current.CancellationToken);

        await NewSeeder(db).SeedAsync(Passwords, TestContext.Current.CancellationToken);

        Assert.Equal(1, await db.Users.CountAsync(user => user.Email == SeedData.AdminEmail, TestContext.Current.CancellationToken));
        Assert.Equal(1, await db.LedgerAccounts.CountAsync(
            account => account.Code == LedgerAccountCodes.SettlementFloat, TestContext.Current.CancellationToken));
        Assert.Equal(3, await db.Roles.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(6, await db.SystemSettings.CountAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("", "Operator-pass-for-tests-2!", "Customer-pass-for-tests-3!", "Seed:AdminPassword")]
    [InlineData("Admin-pass-for-tests-1!", "", "Customer-pass-for-tests-3!", "Seed:OperatorPassword")]
    [InlineData("Admin-pass-for-tests-1!", "Operator-pass-for-tests-2!", "short", "Seed:CustomerPassword")]
    public async Task Seed_refuses_to_run_without_a_usable_password(
        string admin, string operatorPassword, string customer, string expectedKey)
    {
        await using var db = sql.NewContext();
        var options = new SeedOptions { AdminPassword = admin, OperatorPassword = operatorPassword, CustomerPassword = customer };

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => NewSeeder(db).SeedAsync(options, TestContext.Current.CancellationToken));

        Assert.Contains(expectedKey, error.Message);
    }
}
