using System.Data;
using LedgerPay.Application.Abstractions;
using LedgerPay.Application.Transactions;
using LedgerPay.Domain.Entities;
using Microsoft.Data.SqlClient;
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

    // The view has the running balance. The joins add what a history line shows. The counterparty is the other wallet
    // of the transaction, and a top-up has none. The wallet id is the only filter in here and it is a parameter.
    public IQueryable<StatementRow> WalletStatement(Guid walletId) => Database.SqlQuery<StatementRow>($"""
        SELECT s.Sequence, s.CreatedAt, s.Debit, s.BalanceAfter,
               t.Reference, t.Type, t.Status, t.Amount, t.Fee, t.Note,
               counterparty.FullName AS CounterpartyName
        FROM [dbo].[vw_WalletStatement] AS s
        INNER JOIN [dbo].[Transactions] AS t ON t.Id = s.TransactionId
        LEFT JOIN [dbo].[Wallets] AS other
            ON other.Id = CASE WHEN t.SenderWalletId = s.WalletId THEN t.ReceiverWalletId ELSE t.SenderWalletId END
        LEFT JOIN [dbo].[Users] AS counterparty ON counterparty.Id = other.UserId
        WHERE s.WalletId = {walletId}
        """);

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

    public async Task<User?> LockUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        // Same as for a wallet: drop a copy the context already holds, then read the row under the lock.
        foreach (var entry in ChangeTracker.Entries<User>().Where(entry => entry.Entity.Id == userId).ToList())
        {
            entry.State = EntityState.Detached;
        }

        var rows = await Users
            .FromSql($"SELECT * FROM [Users] WITH (UPDLOCK, ROWLOCK) WHERE [Id] = {userId}")
            .ToListAsync(cancellationToken);

        return rows.SingleOrDefault();
    }

    public async Task LockResourceAsync(string resource, CancellationToken cancellationToken)
    {
        // sp_getapplock holds the lock until the transaction ends. A negative result means it was not granted.
        var result = new SqlParameter("result", SqlDbType.Int) { Direction = ParameterDirection.Output };
        await Database.ExecuteSqlRawAsync(
            "EXEC @result = sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000",
            [result, new SqlParameter("resource", resource)],
            cancellationToken);

        if ((int)result.Value < 0)
        {
            throw new InvalidOperationException($"Could not lock {resource} (code {result.Value}).");
        }
    }

    // 2601 and 2627 are SQL Server's errors for a value that a unique index already holds.
    public bool IsDuplicateKey(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
