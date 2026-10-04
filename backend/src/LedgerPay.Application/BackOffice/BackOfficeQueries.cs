using LedgerPay.Application.Abstractions;
using LedgerPay.Application.Common;
using LedgerPay.Application.Transactions;
using LedgerPay.Domain.Constants;
using LedgerPay.Domain.Entities;
using LedgerPay.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LedgerPay.Application.BackOffice;

public sealed class BackOfficeQueries(IAppDbContext db, TimeProvider clock) : IBackOfficeQueries
{
    // A person's page shows the latest few, and the transactions list is where the rest are.
    private const int RecentCount = 10;

    public async Task<PagedResponse<UserSummary>> ListUsersAsync(UserSearchQuery query, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var wallets = db.Wallets.AsNoTracking();

        if (query.Search?.Trim() is { Length: > 0 } term)
        {
            // Contains is sent as a parameter, so a percent sign or an underscore in the text is only text.
            wallets = wallets.Where(wallet =>
                wallet.WalletNumber.Contains(term) || wallet.User.FullName.Contains(term) ||
                wallet.User.Email.Contains(term) || wallet.User.Phone.Contains(term));
        }

        wallets = query.Status switch
        {
            UserStatusFilter.Frozen => wallets.Where(wallet => wallet.Status == WalletStatus.Frozen),
            UserStatusFilter.Locked => wallets.Where(wallet => wallet.User.LockoutEnd > now),
            UserStatusFilter.Active => wallets.Where(wallet =>
                wallet.Status == WalletStatus.Active && (wallet.User.LockoutEnd == null || wallet.User.LockoutEnd <= now)),
            _ => wallets
        };

        var totalCount = await wallets.CountAsync(cancellationToken);
        var rows = await wallets
            .OrderBy(wallet => wallet.User.FullName).ThenBy(wallet => wallet.WalletNumber)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(wallet => new
            {
                wallet.WalletNumber, wallet.User.FullName, wallet.User.Email, wallet.User.Phone, wallet.Balance,
                wallet.Status, Locked = wallet.User.LockoutEnd > now, wallet.User.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(row => new UserSummary(row.WalletNumber, row.FullName, row.Email, row.Phone, row.Balance, row.Status, row.Locked, Utc(row.CreatedAt)))
            .ToList();
        return PagedResponse.Create(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<ServiceResult<UserDetail>> GetUserAsync(
        Guid actorUserId, string walletNumber, RequestInfo info, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var found = await db.Wallets.AsNoTracking()
            .Where(wallet => wallet.WalletNumber == walletNumber)
            .Select(wallet => new
            {
                wallet.Id, wallet.WalletNumber, wallet.User.FullName, wallet.User.Email, wallet.User.Phone, wallet.Balance,
                wallet.Status, wallet.StatusReason, wallet.StatusChangedAt, wallet.StatusChangedByUserId,
                wallet.User.LockoutEnd, wallet.User.FailedLoginCount, wallet.User.CreatedAt
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (found is null)
        {
            return ServiceResult<UserDetail>.Fail(ErrorCodes.WalletNotFound);
        }

        var changedBy = found.StatusChangedByUserId is { } changer
            ? await db.Users.AsNoTracking().Where(user => user.Id == changer).Select(user => user.FullName).SingleOrDefaultAsync(cancellationToken)
            : null;

        var recent = await StaffTransactions(db.Transactions.AsNoTracking()
                .Where(transaction => transaction.SenderWalletId == found.Id || transaction.ReceiverWalletId == found.Id))
            .Take(RecentCount)
            .ToListAsync(cancellationToken);

        db.AuditLogs.Add(AuditEntry.Create(
            AuditActions.UserViewed, AuditEntityTypes.Wallet, found.WalletNumber, null, actorUserId, info, now));
        await db.SaveChangesAsync(cancellationToken);

        var locked = found.LockoutEnd > now;
        return ServiceResult<UserDetail>.Ok(new UserDetail(
            found.WalletNumber, found.FullName, found.Email, found.Phone, found.Balance, found.Status, found.StatusReason,
            found.StatusChangedAt is { } changedAt ? Utc(changedAt) : null, changedBy,
            locked, locked ? Utc(found.LockoutEnd!.Value) : null, found.FailedLoginCount, Utc(found.CreatedAt),
            recent.Select(ToItem).ToList()));
    }

    public async Task<PagedResponse<StaffTransactionItem>> ListTransactionsAsync(StaffTransactionQuery query, CancellationToken cancellationToken)
    {
        var transactions = db.Transactions.AsNoTracking();

        if (query.Type is { } type)
        {
            transactions = transactions.Where(transaction => transaction.Type == type);
        }

        if (query.Status is { } status)
        {
            transactions = transactions.Where(transaction => transaction.Status == status);
        }

        if (query.WalletNumber is { } walletNumber)
        {
            transactions = transactions.Where(transaction =>
                transaction.SenderWallet!.WalletNumber == walletNumber || transaction.ReceiverWallet!.WalletNumber == walletNumber);
        }

        if (query.From is { } from)
        {
            var start = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            transactions = transactions.Where(transaction => transaction.CreatedAt >= start);
        }

        if (query.To is { } to)
        {
            // The last instant of the day, so the largest date cannot overflow by adding a day to it.
            var end = to.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
            transactions = transactions.Where(transaction => transaction.CreatedAt <= end);
        }

        var totalCount = await transactions.CountAsync(cancellationToken);
        var rows = await StaffTransactions(transactions)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return PagedResponse.Create(rows.Select(ToItem).ToList(), query.Page, query.PageSize, totalCount);
    }

    public async Task<PagedResponse<AuditItem>> ListAuditAsync(AuditQuery query, CancellationToken cancellationToken)
    {
        var entries = from log in db.AuditLogs.AsNoTracking()
                      join user in db.Users.AsNoTracking() on log.ActorUserId equals user.Id into actors
                      from actor in actors.DefaultIfEmpty()
                      select new { Log = log, ActorName = actor.FullName, ActorEmail = actor.Email };

        if (query.Action is { } action)
        {
            entries = entries.Where(entry => entry.Log.Action == action);
        }

        if (query.Actor?.Trim() is { Length: > 0 } actorTerm)
        {
            entries = entries.Where(entry => entry.ActorName.Contains(actorTerm) || entry.ActorEmail.Contains(actorTerm));
        }

        if (query.From is { } from)
        {
            var start = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            entries = entries.Where(entry => entry.Log.CreatedAt >= start);
        }

        if (query.To is { } to)
        {
            var end = to.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
            entries = entries.Where(entry => entry.Log.CreatedAt <= end);
        }

        var totalCount = await entries.CountAsync(cancellationToken);
        var rows = await entries
            .OrderByDescending(entry => entry.Log.CreatedAt).ThenByDescending(entry => entry.Log.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(entry => new AuditItem(
                Utc(entry.Log.CreatedAt), entry.Log.Action, entry.Log.EntityType, entry.Log.EntityReference,
                entry.ActorName, entry.ActorEmail, entry.Log.IpAddress, entry.Log.CorrelationId, entry.Log.Details))
            .ToList();
        return PagedResponse.Create(items, query.Page, query.PageSize, totalCount);
    }

    // Newest first, with the two wallets and their holders. The same shape serves the list and a person's page.
    private static IQueryable<StaffRow> StaffTransactions(IQueryable<Transaction> transactions) =>
        transactions
            .OrderByDescending(transaction => transaction.CreatedAt).ThenByDescending(transaction => transaction.Reference)
            .Select(transaction => new StaffRow(
                transaction.Reference, transaction.Type, transaction.Status, transaction.Amount, transaction.Fee, transaction.FailureCode,
                transaction.SenderWallet!.WalletNumber, transaction.SenderWallet!.User.FullName,
                transaction.ReceiverWallet!.WalletNumber, transaction.ReceiverWallet!.User.FullName,
                transaction.RequestedReceiver, transaction.BankReference, transaction.Note, transaction.CreatedAt));

    private static StaffTransactionItem ToItem(StaffRow row) => new(
        row.Reference, row.Type, row.Status, row.Amount, row.Fee, row.FailureCode, row.SenderWalletNumber, row.SenderName,
        row.ReceiverWalletNumber, row.ReceiverName, row.RequestedReceiver, row.BankReference, row.Note, Utc(row.CreatedAt));

    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private sealed record StaffRow(
        string Reference, TransactionType Type, TransactionStatus Status, decimal Amount, decimal Fee, string? FailureCode,
        string? SenderWalletNumber, string? SenderName, string? ReceiverWalletNumber, string? ReceiverName,
        string? RequestedReceiver, string? BankReference, string? Note, DateTime CreatedAt);
}
