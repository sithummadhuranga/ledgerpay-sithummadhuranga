namespace LedgerPay.Api.Extensions;

public static class CorsExtensions
{
    public const string SectionName = "Cors:AllowedOrigins";

    // Only the origins in the configuration may call the API from a browser. A wildcard would let any site do it,
    // so the app refuses to start with one, or with no origin at all.
    public static IServiceCollection AddFrontendCors(this IServiceCollection services, IConfiguration configuration)
    {
        var origins = configuration.GetSection(SectionName).Get<string[]>() ?? [];

        if (origins.Length == 0 || origins.Any(origin => !IsPlainOrigin(origin)))
        {
            throw new InvalidOperationException(
                $"{SectionName} must list the frontend origins, for example http://localhost:5173. A wildcard or an empty value is not allowed.");
        }

        services.AddCors(options => options.AddDefaultPolicy(policy => policy
            .WithOrigins(origins)
            .WithMethods("GET", "POST", "PATCH")
            .WithHeaders("Authorization", "Content-Type", ControllerResultExtensions.IdempotencyKeyHeader, "X-Correlation-Id")
            .WithExposedHeaders("Retry-After", "Idempotent-Replayed", "X-Correlation-Id")
            .SetPreflightMaxAge(TimeSpan.FromMinutes(10))));

        return services;
    }

    // Scheme, host and port, written the way a browser sends them. A trailing slash or a path would never match.
    private static bool IsPlainOrigin(string origin) =>
        Uri.TryCreate(origin, UriKind.Absolute, out var uri)
        && uri.Scheme is "http" or "https"
        && string.IsNullOrEmpty(uri.UserInfo)
        && uri.GetLeftPart(UriPartial.Authority) == origin;
}
