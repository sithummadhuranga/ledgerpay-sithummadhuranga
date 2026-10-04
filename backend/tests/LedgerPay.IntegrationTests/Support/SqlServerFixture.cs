using LedgerPay.Infrastructure.Persistence;
using LedgerPay.Infrastructure.Persistence.Sql;
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

    // A limited login with the same grants the real API login gets. Only the test container ever sees its password.
    public string ApiLoginName => "ledgerpay_api_test";

    public string ApiConnectionString { get; private set; } = string.Empty;

    // One in-memory API for the whole run, made when a test first asks for it.
    private ApiFactory? api;

    public ApiFactory Api => api ??= new ApiFactory(ApiConnectionString, TestJwt.Options());

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

        var apiPassword = "Api-" + Guid.NewGuid().ToString("N") + "-Aa1!";
        await CreateApiLoginAsync(apiPassword);
        await DatabasePermissions.ApplyAsync(db, ApiLoginName, CancellationToken.None);
        ApiConnectionString = new SqlConnectionStringBuilder(AdminConnectionString)
        {
            UserID = ApiLoginName,
            Password = apiPassword
        }.ConnectionString;
    }

    private async Task CreateApiLoginAsync(string password)
    {
        await using var connection = new SqlConnection(ServerConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE LOGIN [{ApiLoginName}] WITH PASSWORD = '{password}', CHECK_POLICY = OFF;";
        await command.ExecuteNonQueryAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (api is not null)
        {
            await api.DisposeAsync();
        }

        await container.DisposeAsync();
    }

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
