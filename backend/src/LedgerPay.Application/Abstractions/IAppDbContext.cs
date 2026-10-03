using LedgerPay.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LedgerPay.Application.Abstractions;

public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<Wallet> Wallets { get; }
    DbSet<LedgerAccount> LedgerAccounts { get; }
    DbSet<Transaction> Transactions { get; }
    DbSet<LedgerEntry> LedgerEntries { get; }
    DbSet<IdempotencyKey> IdempotencyKeys { get; }
    DbSet<SystemSetting> SystemSettings { get; }
    DbSet<AuditLog> AuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    // Runs the work in one database transaction, inside the retrying execution strategy.
    // After a transient fault the strategy runs the whole delegate again, so it must be safe to repeat.
    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken);

    // Reads a wallet row and holds an update lock on it until the transaction ends.
    Task<Wallet?> LockWalletAsync(Guid walletId, CancellationToken cancellationToken);

    // True when a save failed because a unique index already holds the value.
    bool IsDuplicateKey(DbUpdateException exception);
}
