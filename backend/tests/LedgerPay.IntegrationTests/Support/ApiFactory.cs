using LedgerPay.Infrastructure.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LedgerPay.IntegrationTests.Support;

// The whole API in memory, on the test database, connected as the limited login that ships.
public sealed class ApiFactory(string apiConnectionString, JwtOptions jwt, IReadOnlyDictionary<string, string?>? settings = null)
    : WebApplicationFactory<Program>
{
    public const string AllowedOrigin = "http://localhost:5173";

    public JwtOptions Jwt => jwt;

    public LogCollector Logs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseSetting("ConnectionStrings:Api", apiConnectionString);
        builder.UseSetting("Jwt:SigningKey", jwt.SigningKey);
        builder.UseSetting("Jwt:Issuer", jwt.Issuer);
        builder.UseSetting("Jwt:Audience", jwt.Audience);
        builder.UseSetting("Jwt:AccessTokenMinutes", jwt.AccessTokenMinutes.ToString());
        builder.UseSetting("Cors:AllowedOrigins:0", AllowedOrigin);

        // The limits are far above anything the suite sends, so a test only meets a limit it asked for.
        foreach (var area in new[] { "Auth", "Lookup", "Money" })
        {
            builder.UseSetting($"RateLimits:{area}:PermitLimit", "1000000");
        }

        foreach (var (key, value) in settings ?? new Dictionary<string, string?>())
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureLogging(logging => logging.AddProvider(Logs));

        builder.ConfigureServices(services =>
        {
            services.AddControllers().AddApplicationPart(typeof(ProbeController).Assembly);
            services.AddSingleton<IStartupFilter, FakeRemoteIpStartupFilter>();
        });
    }
}
