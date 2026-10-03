using Microsoft.EntityFrameworkCore.Diagnostics;

namespace LedgerPay.IntegrationTests.Support.Faults;

// Fails after the rows are written but before the transaction commits, which is the worst moment for a retry.
internal sealed class FailOnceAfterSave : SaveChangesInterceptor
{
    private int hasFailed;

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref hasFailed, 1) == 0)
        {
            throw new TransientTestException();
        }

        return ValueTask.FromResult(result);
    }
}
