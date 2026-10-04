using LedgerPay.Api.Health;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LedgerPay.Api.Extensions;

public static class HealthExtensions
{
    public const string Path = "/api/v1/health";

    public static IServiceCollection AddApiHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");
        return services;
    }

    // 200 when healthy and 503 when not, with only the word Healthy or Unhealthy in the body. A load balancer or a
    // container platform can poll it without a token.
    public static IEndpointConventionBuilder MapApiHealth(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapHealthChecks(Path, new HealthCheckOptions
        {
            ResponseWriter = (context, report) =>
            {
                context.Response.ContentType = "application/json";
                return context.Response.WriteAsJsonAsync(new { status = report.Status == HealthStatus.Healthy ? "Healthy" : "Unhealthy" });
            }
        }).WithMetadata(new HttpMethodMetadata(["GET", "HEAD"])).AllowAnonymous();
}
