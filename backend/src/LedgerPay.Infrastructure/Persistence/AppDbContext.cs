using LedgerPay.Application.Abstractions;
using LedgerPay.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LedgerPay.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<LedgerAccount> LedgerAccounts => Set<LedgerAccount>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();
    public DbSet<IdempotencyKey> IdempotencyKeys => Set<IdempotencyKey>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken)
    {
        var strategy = Database.CreateExecutionStrategy();

        return strategy.ExecuteAsync(async token =>
        {
            // A retry starts from a clean tracker, otherwise the rows added by the failed attempt would be saved twice.
            ChangeTracker.Clear();

            await using var transaction = await Database.BeginTransactionAsync(token);
            var result = await work(token);
            await transaction.CommitAsync(token);
            return result;
        }, cancellationToken);
    }

    public async Task<Wallet?> LockWalletAsync(Guid walletId, CancellationToken cancellationToken)
    {
        // A copy of this wallet that the context already tracks would be returned as it is, with old values.
        // Detach it so the row that comes back is the one just read under the lock.
        foreach (var entry in ChangeTracker.Entries<Wallet>().Where(entry => entry.Entity.Id == walletId).ToList())
        {
            entry.State = EntityState.Detached;
        }

        // UPDLOCK keeps every other transaction away from this row until this one commits or rolls back.
        var rows = await Wallets
            .FromSql($"SELECT * FROM [Wallets] WITH (UPDLOCK, ROWLOCK) WHERE [Id] = {walletId}")
            .ToListAsync(cancellationToken);

        return rows.SingleOrDefault();
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
