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

    // Azure SQL has no server logins, so the user is made inside the database with its own password. The test database is
    // made with partial containment, which Azure SQL has by default.
    private async Task<(string ConnectionString, string User)> ContainedDatabaseAsync(string password)
    {
        var database = "LedgerPayContained" + Guid.NewGuid().ToString("N")[..12];
        await using (var master = new SqlConnection(sql.ServerConnectionString()))
        {
            await master.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = master.CreateCommand();
            command.CommandText = $"EXEC sp_configure 'contained database authentication', 1; RECONFIGURE; CREATE DATABASE [{database}] CONTAINMENT = PARTIAL;";
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        var connectionString = new SqlConnectionStringBuilder(sql.AdminConnectionString) { InitialCatalog = database }.ConnectionString;
        await using var owner = SqlServerFixture.NewContext(connectionString);
        await owner.Database.MigrateAsync(TestContext.Current.CancellationToken);

        var user = "ledgerpay_contained_" + Guid.NewGuid().ToString("N")[..8];
        await DatabasePermissions.ApplyAsync(owner, user, CancellationToken.None, password);
        return (connectionString, user);
    }

    private static string AsUser(string connectionString, string user, string password) =>
        new SqlConnectionStringBuilder(connectionString) { UserID = user, Password = password, Pooling = false }.ConnectionString;

    [Fact]
    public async Task A_user_made_inside_the_database_gets_the_same_limited_rights_as_a_login_user()
    {
        const string password = "Contained-Pass-2026-Aa1!";
        var (connectionString, user) = await ContainedDatabaseAsync(password);

        await using var api = SqlServerFixture.NewContext(AsUser(connectionString, user, password));

        Assert.True(await api.Database.CanConnectAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await HasPermissionAsync(api, "dbo.LedgerEntries", "OBJECT", "INSERT"));
        Assert.Equal(0, await HasPermissionAsync(api, "dbo.LedgerEntries", "OBJECT", "UPDATE"));
        Assert.Equal(0, await HasPermissionAsync(api, "dbo.LedgerEntries", "OBJECT", "DELETE"));
        Assert.Equal(0, await HasPermissionAsync(api, "dbo.AuditLogs", "OBJECT", "DELETE"));
        Assert.Equal(1, await HasColumnPermissionAsync(api, "dbo.Users", "LockoutEnd", "UPDATE"));
        Assert.Equal(0, await HasColumnPermissionAsync(api, "dbo.Users", "PasswordHash", "UPDATE"));
        Assert.Equal(0, await HasPermissionAsync(api, new SqlConnectionStringBuilder(connectionString).InitialCatalog, "DATABASE", "CREATE TABLE"));
    }

    [Fact]
    public async Task Running_the_setup_again_with_a_new_password_changes_the_password()
    {
        const string first = "Contained-Pass-2026-Aa1!";
        const string second = "Rotated-Pass-2027-Bb2!";
        var (connectionString, user) = await ContainedDatabaseAsync(first);

        await using (var owner = SqlServerFixture.NewContext(connectionString))
        {
            await DatabasePermissions.ApplyAsync(owner, user, CancellationToken.None, second);
        }

        await using var withNew = SqlServerFixture.NewContext(AsUser(connectionString, user, second));
        await using var withOld = SqlServerFixture.NewContext(AsUser(connectionString, user, first));
        Assert.True(await withNew.Database.CanConnectAsync(TestContext.Current.CancellationToken));
        Assert.False(await withOld.Database.CanConnectAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_password_with_a_quote_in_it_is_taken_as_text_and_does_not_break_out_of_the_statement()
    {
        const string password = "It'sA-Pass-2026-Aa1!";
        var (connectionString, user) = await ContainedDatabaseAsync(password);

        await using var api = SqlServerFixture.NewContext(AsUser(connectionString, user, password));

        Assert.True(await api.Database.CanConnectAsync(TestContext.Current.CancellationToken));
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
