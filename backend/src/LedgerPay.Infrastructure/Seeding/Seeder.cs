using LedgerPay.Application.Abstractions;
using LedgerPay.Domain.Constants;
using LedgerPay.Domain.Entities;
using LedgerPay.Domain.Enums;
using LedgerPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LedgerPay.Infrastructure.Seeding;

public sealed class Seeder(AppDbContext db, IPasswordService passwords, TimeProvider clock)
{
    public async Task SeedAsync(SeedOptions options, CancellationToken cancellationToken)
    {
        options.EnsureComplete();

        await SeedRolesAsync(cancellationToken);
        await SeedSettingsAsync(cancellationToken);
        await SeedSystemAccountsAsync(cancellationToken);
        await SeedUserAsync(SeedData.Admin, options.AdminPassword, RoleNames.Admin, cancellationToken);
        await SeedUserAsync(SeedData.Operator, options.OperatorPassword, RoleNames.Operator, cancellationToken);

        foreach (var customer in SeedData.Customers)
        {
            await SeedUserAsync(customer, options.CustomerPassword, RoleNames.Customer, cancellationToken);
        }
    }

    private async Task SeedRolesAsync(CancellationToken cancellationToken)
    {
        foreach (var name in new[] { RoleNames.Customer, RoleNames.Operator, RoleNames.Admin })
        {
            if (!await db.Roles.AnyAsync(role => role.Name == name, cancellationToken))
            {
                db.Roles.Add(new Role { Name = name });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    // Existing rows are left alone, so a limit changed in the database survives a re-seed.
    private async Task SeedSettingsAsync(CancellationToken cancellationToken)
    {
        var defaults = new Dictionary<string, decimal>
        {
            [SettingKeys.FeePercent] = 0.50m,
            [SettingKeys.FeeMinimum] = 10.00m,
            [SettingKeys.FeeMaximum] = 250.00m,
            [SettingKeys.TransferMinimum] = 100.00m,
            [SettingKeys.TransferMaximum] = 500_000.00m,
            [SettingKeys.WalletBalanceCap] = 2_000_000.00m
        };

        foreach (var (key, value) in defaults)
        {
            if (!await db.SystemSettings.AnyAsync(setting => setting.Key == key, cancellationToken))
            {
                db.SystemSettings.Add(new SystemSetting { Key = key, Value = value, UpdatedAt = Now() });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedSystemAccountsAsync(CancellationToken cancellationToken)
    {
        await AddSystemAccountAsync(LedgerAccountCodes.SettlementFloat, "Settlement float", LedgerAccountType.Asset, cancellationToken);
        await AddSystemAccountAsync(LedgerAccountCodes.FeeRevenue, "Fee revenue", LedgerAccountType.Revenue, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task AddSystemAccountAsync(
        string code, string name, LedgerAccountType type, CancellationToken cancellationToken)
    {
        if (!await db.LedgerAccounts.AnyAsync(account => account.Code == code, cancellationToken))
        {
            db.LedgerAccounts.Add(new LedgerAccount { Code = code, Name = name, Type = type });
        }
    }

    private async Task SeedUserAsync(
        SeedUser seedUser, string password, string roleName, CancellationToken cancellationToken)
    {
        if (await db.Users.AnyAsync(user => user.Email == seedUser.Email, cancellationToken))
        {
            return;
        }

        var role = await db.Roles.SingleAsync(role => role.Name == roleName, cancellationToken);
        var user = new User
        {
            Email = seedUser.Email,
            Phone = seedUser.Phone,
            FullName = seedUser.FullName,
            PasswordHash = passwords.Hash(password),
            CreatedAt = Now()
        };
        user.UserRoles.Add(new UserRole { User = user, Role = role });

        if (roleName == RoleNames.Customer)
        {
            AddWalletWithLedgerAccount(user);
        }

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
    }

    private void AddWalletWithLedgerAccount(User user)
    {
        var wallet = Wallet.Open(user, Now());
        user.Wallet = wallet;
        db.LedgerAccounts.Add(LedgerAccount.ForWallet(wallet));
    }

    private DateTime Now() => clock.GetUtcNow().UtcDateTime;
}
