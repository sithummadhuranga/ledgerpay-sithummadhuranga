using LedgerPay.Application.Idempotency;
using LedgerPay.Application.Settings;
using LedgerPay.Application.Transfers;
using LedgerPay.Infrastructure.Persistence;

namespace LedgerPay.IntegrationTests.Support;

// Builds the real services over a real context, the way the container would.
internal static class TestServices
{
    public static string NewKey() => Guid.NewGuid().ToString();

    public static TransferService Transfers(AppDbContext db, TimeProvider? clock = null)
    {
        clock ??= TimeProvider.System;
        return new TransferService(db, new LedgerSettingsProvider(db), new IdempotencyService(db, clock), clock);
    }
}
