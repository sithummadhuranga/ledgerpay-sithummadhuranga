using Microsoft.EntityFrameworkCore.Storage;

namespace LedgerPay.IntegrationTests.Support.Faults;

// Retries only the fault the tests raise themselves, the way the SQL Server strategy retries a dropped connection.
internal sealed class TransientRetryStrategy(ExecutionStrategyDependencies dependencies)
    : ExecutionStrategy(dependencies, maxRetryCount: 3, maxRetryDelay: TimeSpan.FromMilliseconds(10))
{
    protected override bool ShouldRetryOn(Exception exception) => exception is TransientTestException;
}
