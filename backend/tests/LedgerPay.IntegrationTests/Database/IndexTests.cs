using LedgerPay.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace LedgerPay.IntegrationTests.Database;

public class IndexTests(SqlServerFixture sql)
{
    [Fact]
    public async Task Ledger_entries_are_indexed_by_account_and_append_sequence()
    {
        await using var db = sql.NewContext();

        var keyColumns = await db.Database.SqlQuery<string>($"""
            SELECT c.name AS [Value]
            FROM sys.indexes AS i
            INNER JOIN sys.index_columns AS ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            INNER JOIN sys.columns AS c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            WHERE i.object_id = OBJECT_ID('dbo.LedgerEntries')
              AND i.name = 'IX_LedgerEntries_LedgerAccountId_Sequence'
              AND ic.is_included_column = 0
            ORDER BY ic.key_ordinal
            """).ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["LedgerAccountId", "Sequence"], keyColumns);
    }

    [Theory]
    [InlineData("IX_RefreshTokens_TokenHash", true, new[] { "TokenHash" })]
    [InlineData("IX_RefreshTokens_FamilyId_UserId", false, new[] { "FamilyId", "UserId" })]
    [InlineData("IX_RefreshTokens_UserId", false, new[] { "UserId" })]
    public async Task Refresh_tokens_are_indexed_for_the_ways_they_are_looked_up(string index, bool unique, string[] columns)
    {
        await using var db = sql.NewContext();

        var keyColumns = await db.Database.SqlQuery<string>($"""
            SELECT c.name AS [Value]
            FROM sys.indexes AS i
            INNER JOIN sys.index_columns AS ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            INNER JOIN sys.columns AS c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            WHERE i.object_id = OBJECT_ID('dbo.RefreshTokens') AND i.name = {index} AND ic.is_included_column = 0
            ORDER BY ic.key_ordinal
            """).ToListAsync(TestContext.Current.CancellationToken);
        var isUnique = await db.Database.SqlQuery<bool>($"SELECT i.is_unique AS [Value] FROM sys.indexes AS i WHERE i.object_id = OBJECT_ID('dbo.RefreshTokens') AND i.name = {index}")
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(columns, keyColumns);
        Assert.Equal(unique, isUnique);
    }
}
