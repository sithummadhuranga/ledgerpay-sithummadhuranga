using LedgerPay.Application.Abstractions;
using LedgerPay.Application.Common;
using LedgerPay.Domain.Constants;
using LedgerPay.Domain.Enums;
using LedgerPay.Domain.Rules;
using Microsoft.EntityFrameworkCore;

namespace LedgerPay.Application.Transactions;

public sealed class TransactionQueries(IAppDbContext db) : ITransactionQueries
{
    // The longest reference we make is 16 characters. Anything longer, or blank, cannot match, so it is not looked up.
    private const int LongestReference = 40;

    public async Task<ServiceResult<PagedResponse<HistoryItem>>> HistoryAsync(
        Guid userId, HistoryQuery query, CancellationToken cancellationToken)
    {
        var walletId = await db.Wallets.AsNoTracking()
            .Where(wallet => wallet.UserId == userId)
            .Select(wallet => (Guid?)wallet.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (walletId is null)
        {
            return ServiceResult<PagedResponse<HistoryItem>>.Fail(ErrorCodes.WalletNotFound);
        }

        // The dates cut the list after the view has worked out the running balance, so the balance after an entry
        // still counts everything before it, not only the entries inside the dates.
        var rows = db.WalletStatement(walletId.Value);
        if (query.From is { } from)
        {
            var start = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            rows = rows.Where(row => row.CreatedAt >= start);
        }

        if (query.To is { } to)
        {
            var end = to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            rows = rows.Where(row => row.CreatedAt < end);
        }

        var totalCount = await rows.CountAsync(cancellationToken);
        var page = await rows
            .OrderByDescending(row => row.Sequence)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var items = page.Select(ToItem).ToList();
        return ServiceResult<PagedResponse<HistoryItem>>.Ok(PagedResponse.Create(items, query.Page, query.PageSize, totalCount));
    }

    public async Task<ServiceResult<TransactionResponse>> GetByReferenceAsync(
        Guid userId, bool isBackOffice, string reference, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reference) || reference.Length > LongestReference)
        {
            return ServiceResult<TransactionResponse>.Fail(ErrorCodes.TransactionNotFound);
        }

        var transaction = await db.Transactions.AsNoTracking()
            .Where(candidate => candidate.Reference == reference)
            .Select(candidate => new
            {
                candidate.Reference,
                candidate.Type,
                candidate.Status,
                candidate.Amount,
                candidate.Fee,
                candidate.Note,
                candidate.FailureCode,
                candidate.BankReference,
                candidate.CreatedAt,
                candidate.InitiatedByUserId,
                SenderUserId = (Guid?)candidate.SenderWallet!.UserId,
                SenderName = candidate.SenderWallet!.User.FullName,
                SenderWalletNumber = candidate.SenderWallet!.WalletNumber,
                ReceiverUserId = (Guid?)candidate.ReceiverWallet!.UserId,
                ReceiverName = candidate.ReceiverWallet!.User.FullName,
                ReceiverWalletNumber = candidate.ReceiverWallet!.WalletNumber
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (transaction is null)
        {
            return ServiceResult<TransactionResponse>.Fail(ErrorCodes.TransactionNotFound);
        }

        if (isBackOffice)
        {
            return ServiceResult<TransactionResponse>.Ok(new TransactionResponse(
                transaction.Reference, transaction.Type, transaction.Status, null, transaction.Amount, transaction.Fee,
                null, transaction.Note, transaction.FailureCode, Utc(transaction.CreatedAt),
                transaction.SenderWalletNumber, transaction.ReceiverWalletNumber, transaction.BankReference));
        }

        // A completed transaction belongs to the two wallet holders. A failed one has no entries and may name a
        // wallet that never took part, so it belongs to the customer who tried it. Anyone else gets the same
        // answer as for a reference that does not exist.
        var isSender = transaction.SenderUserId == userId;
        var isReceiver = transaction.ReceiverUserId == userId;
        var isOwner = transaction.Status == TransactionStatus.Completed
            ? isSender || isReceiver
            : transaction.InitiatedByUserId == userId;
        if (!isOwner)
        {
            return ServiceResult<TransactionResponse>.Fail(ErrorCodes.TransactionNotFound);
        }

        var sent = isSender;
        var counterpartyName = sent ? transaction.ReceiverName : transaction.SenderName;
        return ServiceResult<TransactionResponse>.Ok(new TransactionResponse(
            transaction.Reference, transaction.Type, transaction.Status,
            sent ? TransactionDirection.Sent : TransactionDirection.Received,
            transaction.Amount, sent ? transaction.Fee : 0m,
            counterpartyName is null ? null : NameMask.Of(counterpartyName),
            transaction.Note, transaction.FailureCode, Utc(transaction.CreatedAt), null, null, null));
    }

    private static HistoryItem ToItem(StatementRow row)
    {
        // A debit on the wallet account is money going out. The fee is the sender's, so the receiver never sees it.
        var sent = row.Debit > 0;
        return new HistoryItem(
            row.Reference,
            Enum.Parse<TransactionType>(row.Type),
            sent ? TransactionDirection.Sent : TransactionDirection.Received,
            row.Amount,
            sent ? row.Fee : 0m,
            row.CounterpartyName is null ? null : NameMask.Of(row.CounterpartyName),
            row.Note,
            Enum.Parse<TransactionStatus>(row.Status),
            Utc(row.CreatedAt),
            row.BalanceAfter);
    }

    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
