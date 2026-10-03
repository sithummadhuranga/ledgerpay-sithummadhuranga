using LedgerPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace LedgerPay.IntegrationTests.Support.Faults;

internal static class FaultyContext
{
    // A context that retries the one fault the tests raise, the way the real one retries a dropped connection.
    public static AppDbContext Create(string connectionString, IInterceptor interceptor)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString, server => server.ExecutionStrategy(dependencies => new TransientRetryStrategy(dependencies)))
            .AddInterceptors(interceptor)
            .Options;
        return new AppDbContext(options);
    }
}
