using System.Diagnostics;
using LedgerPay.Api.Health;
using LedgerPay.Application.Abstractions;
using LedgerPay.Application.Transactions;
using LedgerPay.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LedgerPay.IntegrationTests.Api;

public class HealthCheckTimeoutTests
{
    // Only CanConnectAsync is used by the health check. The rest of the context is not touched.
    private sealed class FakeDatabase(Func<CancellationToken, Task<bool>> canConnect) : IAppDbContext
    {
        public DbSet<User> Users => throw new NotSupportedException();
        public DbSet<Role> Roles => throw new NotSupportedException();
        public DbSet<UserRole> UserRoles => throw new NotSupportedException();
        public DbSet<Wallet> Wallets => throw new NotSupportedException();
        public DbSet<LedgerAccount> LedgerAccounts => throw new NotSupportedException();
        public DbSet<Transaction> Transactions => throw new NotSupportedException();
        public DbSet<LedgerEntry> LedgerEntries => throw new NotSupportedException();
        public DbSet<IdempotencyKey> IdempotencyKeys => throw new NotSupportedException();
        public DbSet<SystemSetting> SystemSettings => throw new NotSupportedException();
        public DbSet<AuditLog> AuditLogs => throw new NotSupportedException();

        public IQueryable<StatementRow> WalletStatement(Guid walletId) => throw new NotSupportedException();

        public Task<bool> CanConnectAsync(CancellationToken cancellationToken) => canConnect(cancellationToken);

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Wallet?> LockWalletAsync(Guid walletId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<User?> LockUserAsync(Guid userId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task LockResourceAsync(string resource, CancellationToken cancellationToken) => throw new NotSupportedException();

        public bool IsDuplicateKey(DbUpdateException exception) => throw new NotSupportedException();
    }

    private static Task<HealthCheckResult> RunAsync(FakeDatabase database, TimeSpan? timeout = null, CancellationToken cancellationToken = default) =>
        new DatabaseHealthCheck(database, timeout).CheckHealthAsync(new HealthCheckContext(), cancellationToken);

    [Fact]
    public async Task A_database_that_answers_is_healthy()
    {
        var result = await RunAsync(new FakeDatabase(_ => Task.FromResult(true)), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task A_database_that_says_it_cannot_be_reached_is_unhealthy()
    {
        var result = await RunAsync(new FakeDatabase(_ => Task.FromResult(false)), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task A_database_that_does_not_answer_in_time_is_unhealthy_and_the_check_returns_at_once()
    {
        var hangs = new FakeDatabase(async token =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return true;
        });
        var clock = Stopwatch.StartNew();

        var result = await RunAsync(hangs, TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(3), $"took {clock.Elapsed}");
    }

    [Fact]
    public async Task The_default_wait_is_a_few_seconds_so_a_probe_does_not_time_out_first()
    {
        Assert.InRange(DatabaseHealthCheck.DefaultTimeout, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task A_caller_that_gives_up_is_not_told_the_database_is_down()
    {
        using var caller = new CancellationTokenSource();
        var hangs = new FakeDatabase(async token =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return true;
        });
        var check = RunAsync(hangs, TimeSpan.FromSeconds(30), caller.Token);

        await caller.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => check);
    }
}
