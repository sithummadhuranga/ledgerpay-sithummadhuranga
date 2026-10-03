using LedgerPay.Infrastructure.Persistence;
using LedgerPay.Infrastructure.Security;
using LedgerPay.Infrastructure.Seeding;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

[assembly: AssemblyFixture(typeof(LedgerPay.IntegrationTests.Support.SqlServerFixture))]

namespace LedgerPay.IntegrationTests.Support;

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

        // The settings and the two system accounts are what every money test needs.
        await new Seeder(db, new PasswordService(), TimeProvider.System).SeedAsync(TestSeedOptions, CancellationToken.None);
    }

    public ValueTask DisposeAsync() => container.DisposeAsync();

    // A database of its own, for tests that change shared settings and would disturb the others.
    public async Task<string> CreateIsolatedDatabaseAsync()
    {
        var builder = new SqlConnectionStringBuilder(AdminConnectionString)
        {
            InitialCatalog = "LedgerPayIsolated" + Guid.NewGuid().ToString("N")[..12]
        };

        await using var db = NewContext(builder.ConnectionString);
        await db.Database.MigrateAsync();
        await new Seeder(db, new PasswordService(), TimeProvider.System).SeedAsync(TestSeedOptions, CancellationToken.None);
        return builder.ConnectionString;
    }

    private static readonly SeedOptions TestSeedOptions = new()
    {
        AdminPassword = "Admin-pass-for-tests-1!",
        OperatorPassword = "Operator-pass-for-tests-2!",
        CustomerPassword = "Customer-pass-for-tests-3!"
    };

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
