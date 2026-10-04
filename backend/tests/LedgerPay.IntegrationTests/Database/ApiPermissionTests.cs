using LedgerPay.Infrastructure.Persistence;
using LedgerPay.Infrastructure.Persistence.Sql;
using LedgerPay.IntegrationTests.Support;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LedgerPay.IntegrationTests.Database;

public class ApiPermissionTests(SqlServerFixture sql)
{
    [Fact]
    public async Task Api_login_can_insert_ledger_entries_but_not_update_or_delete_them()
    {
        await using var owner = sql.NewContext();
        await DatabasePermissions.ApplyAsync(owner, sql.ApiLoginName, CancellationToken.None);
        var (user, _, account) = await TestData.AddCustomerAsync(owner);
        var transaction = await TestData.AddTransactionAsync(owner, user.Id);

        await using var api = SqlServerFixture.NewContext(sql.ApiConnectionString);
        var entry = await TestData.AddEntryAsync(api, transaction.Id, account.Id, 0m, 500.00m);

        var update = await TestData.CaptureSqlErrorAsync(() =>
            TestData.ExecuteAsync(api, $"UPDATE LedgerEntries SET Credit = 900.00 WHERE Id = {entry.Id}"));
        var delete = await TestData.CaptureSqlErrorAsync(() =>
            TestData.ExecuteAsync(api, $"DELETE FROM LedgerEntries WHERE Id = {entry.Id}"));

        Assert.Contains("permission was denied", update.Message);
        Assert.Contains("permission was denied", delete.Message);
    }

    // Asks SQL Server about the rights instead of running DDL, which would fight the other tests for locks.
    [Fact]
    public async Task Api_login_cannot_alter_the_append_only_tables_or_create_tables()
    {
        await using var owner = sql.NewContext();
        await DatabasePermissions.ApplyAsync(owner, sql.ApiLoginName, CancellationToken.None);

        await using var api = SqlServerFixture.NewContext(sql.ApiConnectionString);
        var alterEntries = await HasPermissionAsync(api, "dbo.LedgerEntries", "OBJECT", "ALTER");
        var alterAudit = await HasPermissionAsync(api, "dbo.AuditLogs", "OBJECT", "ALTER");
        var createTable = await HasPermissionAsync(api, "LedgerPayTests", "DATABASE", "CREATE TABLE");

        Assert.Equal(0, alterEntries);
        Assert.Equal(0, alterAudit);
        Assert.Equal(0, createTable);
    }

    [Fact]
    public async Task Api_login_cannot_update_or_delete_audit_logs()
    {
        await using var owner = sql.NewContext();
        await DatabasePermissions.ApplyAsync(owner, sql.ApiLoginName, CancellationToken.None);

        await using var api = SqlServerFixture.NewContext(sql.ApiConnectionString);
        var canInsert = await HasPermissionAsync(api, "dbo.AuditLogs", "OBJECT", "INSERT");
        var canUpdate = await HasPermissionAsync(api, "dbo.AuditLogs", "OBJECT", "UPDATE");
        var canDelete = await HasPermissionAsync(api, "dbo.AuditLogs", "OBJECT", "DELETE");

        Assert.Equal(1, canInsert);
        Assert.Equal(0, canUpdate);
        Assert.Equal(0, canDelete);
    }

    [Fact]
    public async Task Api_login_can_insert_and_update_only_the_tables_the_app_writes()
    {
        await using var owner = sql.NewContext();
        await DatabasePermissions.ApplyAsync(owner, sql.ApiLoginName, CancellationToken.None);

        await using var api = SqlServerFixture.NewContext(sql.ApiConnectionString);

        foreach (var table in new[] { "Users", "UserRoles", "Wallets", "LedgerAccounts", "Transactions", "LedgerEntries", "IdempotencyKeys", "AuditLogs", "RefreshTokens" })
        {
            Assert.Equal(1, await HasPermissionAsync(api, "dbo." + table, "OBJECT", "INSERT"));
        }

        foreach (var table in new[] { "Roles", "SystemSettings", "__EFMigrationsHistory" })
        {
            Assert.Equal(0, await HasPermissionAsync(api, "dbo." + table, "OBJECT", "INSERT"));
        }

        foreach (var table in new[] { "Wallets", "IdempotencyKeys" })
        {
            Assert.Equal(1, await HasPermissionAsync(api, "dbo." + table, "OBJECT", "UPDATE"));
        }

        foreach (var table in new[] { "Roles", "UserRoles", "LedgerAccounts", "Transactions", "SystemSettings", "LedgerEntries", "AuditLogs" })
        {
            Assert.Equal(0, await HasPermissionAsync(api, "dbo." + table, "OBJECT", "UPDATE"));
        }
    }

    [Fact]
    public async Task Api_login_can_change_only_the_sign_in_and_restriction_columns_of_a_user()
    {
        await using var owner = sql.NewContext();
        await DatabasePermissions.ApplyAsync(owner, sql.ApiLoginName, CancellationToken.None);

        await using var api = SqlServerFixture.NewContext(sql.ApiConnectionString);

        foreach (var column in new[] { "FailedLoginCount", "LockoutEnd", "RestrictedAt", "RestrictedReason", "RestrictedByUserId" })
        {
            Assert.Equal(1, await HasColumnPermissionAsync(api, "dbo.Users", column, "UPDATE"));
        }

        foreach (var column in new[] { "Id", "Email", "Phone", "FullName", "PasswordHash", "CreatedAt" })
        {
            Assert.Equal(0, await HasColumnPermissionAsync(api, "dbo.Users", column, "UPDATE"));
        }
    }

    [Fact]
    public async Task Api_login_can_change_only_the_two_revoke_columns_of_a_refresh_token()
    {
        await using var owner = sql.NewContext();
        await DatabasePermissions.ApplyAsync(owner, sql.ApiLoginName, CancellationToken.None);

        await using var api = SqlServerFixture.NewContext(sql.ApiConnectionString);

        foreach (var column in new[] { "RevokedAt", "ReplacedById" })
        {
            Assert.Equal(1, await HasColumnPermissionAsync(api, "dbo.RefreshTokens", column, "UPDATE"));
        }

        foreach (var column in new[] { "Id", "UserId", "FamilyId", "TokenHash", "CreatedAt", "SessionStartedAt", "ExpiresAt", "IpAddress", "UserAgent" })
        {
            Assert.Equal(0, await HasColumnPermissionAsync(api, "dbo.RefreshTokens", column, "UPDATE"));
        }
    }

    [Fact]
    public async Task Api_login_cannot_delete_refresh_tokens_so_a_revoked_one_stays_on_record()
    {
        await using var owner = sql.NewContext();
        await DatabasePermissions.ApplyAsync(owner, sql.ApiLoginName, CancellationToken.None);

        await using var api = SqlServerFixture.NewContext(sql.ApiConnectionString);

        Assert.Equal(0, await HasPermissionAsync(api, "dbo.RefreshTokens", "OBJECT", "DELETE"));
    }

    [Fact]
    public async Task Applying_permissions_twice_does_not_fail()
    {
        await using var owner = sql.NewContext();

        await DatabasePermissions.ApplyAsync(owner, sql.ApiLoginName, CancellationToken.None);
        await DatabasePermissions.ApplyAsync(owner, sql.ApiLoginName, CancellationToken.None);
    }

    private static async Task<int> HasColumnPermissionAsync(
        Infrastructure.Persistence.AppDbContext db, string table, string column, string permission)
    {
        var result = await db.Database
            .SqlQuery<int?>($"SELECT HAS_PERMS_BY_NAME({table}, 'OBJECT', {permission}, {column}, 'COLUMN') AS [Value]")
            .ToListAsync(TestContext.Current.CancellationToken);
        return result.Single() ?? 0;
    }

    private static async Task<int> HasPermissionAsync(
        Infrastructure.Persistence.AppDbContext db, string securable, string securableClass, string permission)
    {
        var result = await db.Database
            .SqlQuery<int?>($"SELECT HAS_PERMS_BY_NAME({securable}, {securableClass}, {permission}) AS [Value]")
            .ToListAsync(TestContext.Current.CancellationToken);
        return result.Single() ?? 0;
    }
}
