using Microsoft.EntityFrameworkCore.Diagnostics;

namespace LedgerPay.IntegrationTests.Support.Faults;

// Fails once, right after the second save of a money operation. The first save writes the idempotency key
// and the second writes the transfer, the entries and the stored response, so the rows are in the database
// but the transaction has not committed. That is the worst moment for a retry.
internal sealed class FailOnceAfterSave : SaveChangesInterceptor
{
    private int saves;

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (Interlocked.Increment(ref saves) == 2)
        {
            throw new TransientTestException();
        }

        return ValueTask.FromResult(result);
    }
}
