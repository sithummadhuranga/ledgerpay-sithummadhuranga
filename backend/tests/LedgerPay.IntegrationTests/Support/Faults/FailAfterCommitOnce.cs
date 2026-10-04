using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace LedgerPay.IntegrationTests.Support.Faults;

// Fails once right after the database has committed. The caller sees an error although the work is done,
// like a connection that drops while the reply to COMMIT is on its way back.
internal sealed class FailAfterCommitOnce : DbTransactionInterceptor
{
    private int hasFailed;

    public override Task TransactionCommittedAsync(
        DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref hasFailed, 1) == 0)
        {
            throw new TransientTestException();
        }

        return Task.CompletedTask;
    }
}
