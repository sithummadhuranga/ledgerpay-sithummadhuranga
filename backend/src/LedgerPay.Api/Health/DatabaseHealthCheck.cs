using LedgerPay.Application.Abstractions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LedgerPay.Api.Health;

// Healthy when the API can open a connection to its database within a few seconds. A database that does not
// answer counts as down, so the route says 503 at once and a probe that waits for it is not left hanging.
// The reason for a failure is not given to the caller.
public sealed class DatabaseHealthCheck(IAppDbContext db, TimeSpan? timeout = null) : IHealthCheck
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(3);

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(timeout ?? DefaultTimeout);

        try
        {
            return await db.CanConnectAsync(limit.Token) ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy();
        }
    }
}
