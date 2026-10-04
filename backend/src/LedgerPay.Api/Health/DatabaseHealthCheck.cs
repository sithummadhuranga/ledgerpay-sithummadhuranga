using LedgerPay.Application.Abstractions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LedgerPay.Api.Health;

// Healthy when the API can open a connection to its database. The reason for a failure is not given to the caller.
public sealed class DatabaseHealthCheck(IAppDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        await db.CanConnectAsync(cancellationToken) ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy();
}
