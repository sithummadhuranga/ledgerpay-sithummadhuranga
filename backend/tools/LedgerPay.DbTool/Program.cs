using LedgerPay.Infrastructure;
using LedgerPay.Infrastructure.Persistence;
using LedgerPay.Infrastructure.Persistence.Sql;
using LedgerPay.Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

const string usage = "Usage: LedgerPay.DbTool <setup|migrate|permissions|seed>";

var command = args.FirstOrDefault();
if (command is not ("setup" or "migrate" or "permissions" or "seed"))
{
    Console.Error.WriteLine(usage);
    return 1;
}

var builder = Host.CreateApplicationBuilder();
builder.Configuration.AddUserSecrets(typeof(Program).Assembly, optional: true);
builder.Logging.SetMinimumLevel(LogLevel.Warning);

var connectionString = builder.Configuration.GetConnectionString("Migration");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("ConnectionStrings:Migration is not set. Use the migration login, never sa or the API login.");
    return 1;
}

builder.Services.AddInfrastructure(connectionString);
using var host = builder.Build();
using var scope = host.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

try
{
    if (command is "setup" or "migrate")
    {
        await db.Database.MigrateAsync();
        Console.WriteLine("Migrations applied.");
    }

    if (command is "setup" or "permissions")
    {
        var apiUser = builder.Configuration["Database:ApiUser"] ?? "ledgerpay_api";

        // Set only for Azure SQL, where the user is made inside the database with this password.
        var apiPassword = builder.Configuration["Database:ApiUserPassword"];
        await DatabasePermissions.ApplyAsync(db, apiUser, CancellationToken.None, string.IsNullOrEmpty(apiPassword) ? null : apiPassword);
        Console.WriteLine($"Permissions applied for {apiUser}{(string.IsNullOrEmpty(apiPassword) ? "" : " as a user of the database")}.");
    }

    if (command is "setup" or "seed")
    {
        var options = builder.Configuration.GetSection(SeedOptions.SectionName).Get<SeedOptions>() ?? new SeedOptions();
        await scope.ServiceProvider.GetRequiredService<Seeder>().SeedAsync(options, CancellationToken.None);
        Console.WriteLine("Seed data applied.");
    }
}
catch (Exception error) when (error is InvalidOperationException or DbUpdateException or Microsoft.Data.SqlClient.SqlException)
{
    Console.Error.WriteLine(error.Message);
    return 1;
}

return 0;
