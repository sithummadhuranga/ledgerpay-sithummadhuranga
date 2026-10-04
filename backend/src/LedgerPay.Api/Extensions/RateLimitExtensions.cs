using System.Threading.RateLimiting;
using LedgerPay.Api.Errors;
using LedgerPay.Api.RateLimiting;
using LedgerPay.Domain.Constants;

namespace LedgerPay.Api.Extensions;

public static class RateLimitExtensions
{
    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(RateLimitOptions.SectionName).Get<RateLimitOptions>() ?? new RateLimitOptions();
        options.EnsureValid();

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.OnRejected = (context, cancellationToken) =>
            {
                var wait = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
                    ? (int)Math.Ceiling(retryAfter.TotalSeconds)
                    : 1;
                return new ValueTask(Problems.WriteAsync(context.HttpContext, ErrorCodes.RateLimited, cancellationToken, wait));
            };

            limiter.AddPolicy(RateLimitPolicies.Auth, http => Window(AddressOf(http), options.Auth));
            limiter.AddPolicy(RateLimitPolicies.Refresh, http => Window(AddressOf(http), options.Refresh));
            limiter.AddPolicy(RateLimitPolicies.Lookup, http => Window(UserOrAddress(http), options.Lookup));
            limiter.AddPolicy(RateLimitPolicies.Money, http => Window(UserOrAddress(http), options.Money));
        });

        return services;
    }

    // Behind a proxy the address is the one the forwarded headers middleware set, if forwarding is switched on.
    // With it off, a client cannot choose its own address by sending a header.
    private static string AddressOf(HttpContext http) => http.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    // The route needs a token, so there is always a user. The address is only a fallback.
    private static string UserOrAddress(HttpContext http) => http.User.UserId()?.ToString() ?? AddressOf(http);

    private static RateLimitPartition<string> Window(string key, RateLimitRule rule) =>
        RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = rule.PermitLimit,
            Window = TimeSpan.FromSeconds(rule.WindowSeconds),
            QueueLimit = 0
        });
}
