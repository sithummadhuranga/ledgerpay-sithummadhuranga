using LedgerPay.Application.Abstractions;
using LedgerPay.Infrastructure.Persistence;
using LedgerPay.Infrastructure.Security;
using LedgerPay.Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LedgerPay.Infrastructure;

public static class DependencyInjection
{
    // Registered apart from AddInfrastructure because only the API signs tokens. The DbTool has no use for a key.
    public static IServiceCollection AddJwtTokens(this IServiceCollection services, JwtOptions options)
    {
        options.EnsureValid();

        services.AddSingleton(options);
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<IRefreshTokenService, RefreshTokenService>();

        return services;
    }

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        // Retry on failure covers the short connection drops Azure SQL is known for.
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));

        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IPasswordService, PasswordService>();
        services.AddScoped<Seeder>();

        return services;
    }
}
