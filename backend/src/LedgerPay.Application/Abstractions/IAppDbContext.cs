using LedgerPay.Application.Transactions;
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
    DbSet<RefreshToken> RefreshTokens { get; }

    // The history of one wallet: its lines from the wallet statement view joined to their transactions and counterparties,
    // plus the transfers its holder sent and had refused. In no particular order.
    // Cut it with Where after this call: a filter then runs after the view has worked out the running balance.
    IQueryable<StatementRow> WalletStatement(Guid walletId);

    Task<bool> CanConnectAsync(CancellationToken cancellationToken);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    // Runs the work in one database transaction, inside the retrying execution strategy. It clears the change
    // tracker first, so nothing the caller loaded before the call is tracked inside it.
    // After a transient fault the strategy runs the whole delegate again, so it must be safe to repeat.
    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken);

    // Reads a wallet row and holds an update lock on it until the transaction ends.
    Task<Wallet?> LockWalletAsync(Guid walletId, CancellationToken cancellationToken);

    // Reads a user row and holds an update lock on it until the transaction ends.
    Task<User?> LockUserAsync(Guid userId, CancellationToken cancellationToken);

    // Holds a named lock until the transaction ends. Two transactions asking for the same name run one after the other.
    Task LockResourceAsync(string resource, CancellationToken cancellationToken);

    // True when a save failed because a unique index already holds the value.
    bool IsDuplicateKey(DbUpdateException exception);
}
