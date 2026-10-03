using LedgerPay.IntegrationTests.Support;

namespace LedgerPay.IntegrationTests.Database;

public class AppendOnlyTests(SqlServerFixture sql)
{
    [Fact]
    public async Task Ledger_entry_cannot_be_updated()
    {
        await using var db = sql.NewContext();
        var (user, _, account) = await TestData.AddCustomerAsync(db);
        var transaction = await TestData.AddTransactionAsync(db, user.Id);
        var entry = await TestData.AddEntryAsync(db, transaction.Id, account.Id, 0m, 500.00m);

        var error = await TestData.CaptureSqlErrorAsync(() =>
            TestData.ExecuteAsync(db, $"UPDATE LedgerEntries SET Credit = 900.00 WHERE Id = {entry.Id}"));

        Assert.Contains("append-only", error.Message);
    }

    [Fact]
    public async Task Ledger_entry_cannot_be_deleted()
    {
        await using var db = sql.NewContext();
        var (user, _, account) = await TestData.AddCustomerAsync(db);
        var transaction = await TestData.AddTransactionAsync(db, user.Id);
        var entry = await TestData.AddEntryAsync(db, transaction.Id, account.Id, 0m, 500.00m);

        var error = await TestData.CaptureSqlErrorAsync(() =>
            TestData.ExecuteAsync(db, $"DELETE FROM LedgerEntries WHERE Id = {entry.Id}"));

        Assert.Contains("append-only", error.Message);
    }

    [Fact]
    public async Task Audit_log_cannot_be_updated_or_deleted()
    {
        await using var db = sql.NewContext();
        var id = Guid.NewGuid();
        await TestData.ExecuteAsync(db,
            $"INSERT INTO AuditLogs (Id, CreatedAt, Action, EntityType) VALUES ({id}, SYSUTCDATETIME(), 'Register', 'User')");

        var update = await TestData.CaptureSqlErrorAsync(() =>
            TestData.ExecuteAsync(db, $"UPDATE AuditLogs SET Action = 'Edited' WHERE Id = {id}"));
        var delete = await TestData.CaptureSqlErrorAsync(() =>
            TestData.ExecuteAsync(db, $"DELETE FROM AuditLogs WHERE Id = {id}"));

        Assert.Contains("append-only", update.Message);
        Assert.Contains("append-only", delete.Message);
    }
}
