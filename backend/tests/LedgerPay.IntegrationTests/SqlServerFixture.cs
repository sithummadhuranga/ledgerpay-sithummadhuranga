using LedgerPay.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

[assembly: AssemblyFixture(typeof(LedgerPay.IntegrationTests.SqlServerFixture))]

namespace LedgerPay.IntegrationTests;

// One SQL Server container for the whole run. Starting one per test is far too slow under Rosetta.
public sealed class SqlServerFixture : IAsyncLifetime
{
    private const string DatabaseName = "LedgerPayTests";

    private readonly MsSqlContainer container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public string AdminConnectionString { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        await container.StartAsync();

        var builder = new SqlConnectionStringBuilder(container.GetConnectionString())
        {
            InitialCatalog = DatabaseName
        };
        AdminConnectionString = builder.ConnectionString;

        await using var db = NewContext();
        await db.Database.MigrateAsync();
    }

    public ValueTask DisposeAsync() => container.DisposeAsync();

    public AppDbContext NewContext() => NewContext(AdminConnectionString);

    public static AppDbContext NewContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connectionString).Options;
        return new AppDbContext(options);
    }

    public string ServerConnectionString()
    {
        var builder = new SqlConnectionStringBuilder(AdminConnectionString) { InitialCatalog = "master" };
        return builder.ConnectionString;
    }
}
