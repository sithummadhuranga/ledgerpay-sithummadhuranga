using LedgerPay.Application.Abstractions;
using LedgerPay.Application.Common;
using LedgerPay.Application.Idempotency;
using LedgerPay.Application.Settings;
using LedgerPay.Domain.Constants;
using LedgerPay.Domain.Entities;
using LedgerPay.Domain.Enums;
using LedgerPay.Domain.Identifiers;
using LedgerPay.Domain.Rules;
using Microsoft.EntityFrameworkCore;

namespace LedgerPay.Application.Transfers;

public sealed class TransferService(
    IAppDbContext db,
    LedgerSettingsProvider settingsProvider,
    IdempotencyService idempotency,
    TimeProvider clock) : ITransferService
{
    public async Task<ServiceResult<QuoteResponse>> QuoteAsync(decimal amount, CancellationToken cancellationToken)
    {
        // The API validates the request first. A quote is not stored anywhere, so it checks the amount again itself.
        if (!MoneyAmount.IsValid(amount))
        {
            return ServiceResult<QuoteResponse>.Fail(ErrorCodes.ValidationFailed);
        }

        var settings = await settingsProvider.GetAsync(cancellationToken);

        if (amount < settings.TransferMinimum)
        {
            return ServiceResult<QuoteResponse>.Fail(ErrorCodes.AmountBelowMinimum);
        }

        if (amount > settings.TransferMaximum)
        {
            return ServiceResult<QuoteResponse>.Fail(ErrorCodes.AmountAboveMaximum);
        }

        var fee = FeeCalculator.Calculate(amount, settings);
        return ServiceResult<QuoteResponse>.Ok(new QuoteResponse(amount, fee, amount + fee));
    }

    public async Task<ServiceResult<TransferResponse>> TransferAsync(
        Guid userId, string? idempotencyKey, TransferRequest request, RequestInfo info, CancellationToken cancellationToken)
    {
        var keyError = IdempotencyService.ValidateKey(idempotencyKey);
        if (keyError is not null)
        {
            return ServiceResult<TransferResponse>.Fail(keyError);
        }

        // The validator already checks this. The service checks it again, because a request with no recipient
        // would otherwise be matched against wallets whose phone is null.
        if ((request.RecipientWalletNumber is null) == (request.RecipientPhone is null))
        {
            return ServiceResult<TransferResponse>.Fail(ErrorCodes.ValidationFailed);
        }

        // These two reads only find the ids. The rows are read again below, under a lock.
        var senderId = await db.Wallets.AsNoTracking()
            .Where(wallet => wallet.UserId == userId)
            .Select(wallet => (Guid?)wallet.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (senderId is null)
        {
            return ServiceResult<TransferResponse>.Fail(ErrorCodes.WalletNotFound);
        }

        var receiverId = await FindReceiverIdAsync(request, cancellationToken);

        return await idempotency.RunAsync(
            userId,
            idempotencyKey,
            IdempotencyEndpoints.Transfers,
            RequestHasher.Hash(request),
            (record, token) => PostTransferAsync(userId, senderId.Value, receiverId, request, info, record, token),
            cancellationToken);
    }

    private async Task<Guid?> FindReceiverIdAsync(TransferRequest request, CancellationToken cancellationToken)
    {
        var wallets = db.Wallets.AsNoTracking();

        if (request.RecipientWalletNumber is not null)
        {
            return await wallets
                .Where(wallet => wallet.WalletNumber == request.RecipientWalletNumber)
                .Select(wallet => (Guid?)wallet.Id)
                .SingleOrDefaultAsync(cancellationToken);
        }

        return await wallets
            .Where(wallet => wallet.User.Phone == request.RecipientPhone)
            .Select(wallet => (Guid?)wallet.Id)
            .SingleOrDefaultAsync(cancellationToken);
    }

    // Runs inside the transaction the idempotency service opened, after it inserted the key.
    // Nothing is saved here: the idempotency service saves once at the end, with the stored answer.
    private async Task<ServiceResult<TransferResponse>> PostTransferAsync(
        Guid userId,
        Guid senderId,
        Guid? receiverId,
        TransferRequest request,
        RequestInfo info,
        IdempotencyKey record,
        CancellationToken cancellationToken)
    {
        var settings = await settingsProvider.GetAsync(cancellationToken);

        // Every transfer locks its wallets in ascending id order. Two opposite transfers then queue up
        // behind each other instead of each holding one wallet and waiting for the other.
        var locked = new Dictionary<Guid, Wallet>();
        foreach (var id in new[] { senderId, receiverId }.OfType<Guid>().Distinct().Order())
        {
            locked[id] = await db.LockWalletAsync(id, cancellationToken)
                ?? throw new InvalidOperationException($"Wallet {id} disappeared during a transfer.");
        }

        var sender = locked[senderId];
        var receiver = receiverId is null ? null : locked[receiverId.Value];
        var fee = FeeCalculator.Calculate(request.Amount, settings);
        var now = clock.GetUtcNow().UtcDateTime;

        var failure = TransferRules.FirstFailure(sender, receiver, request.Amount, fee, settings);
        if (failure is not null)
        {
            // A rejected attempt is kept as a Failed row with no entries, and the key keeps its answer,
            // so the same key gets the same rejection when it is sent again.
            var failed = AddFailedTransfer(userId, sender, receiver, request, info, failure, now);
            record.TransactionId = failed.Id;
            return ServiceResult<TransferResponse>.Fail(failure);
        }

        var completed = await AddCompletedTransferAsync(userId, sender, receiver!, request, info, fee, now, cancellationToken);
        record.TransactionId = completed.Id;

        return ServiceResult<TransferResponse>.Ok(new TransferResponse(
            completed.Reference, request.Amount, fee, request.Amount + fee, sender.Balance, receiver!.WalletNumber, now));
    }

    private Transaction AddFailedTransfer(
        Guid userId, Wallet sender, Wallet? receiver, TransferRequest request, RequestInfo info, string failureCode, DateTime now)
    {
        var transaction = new Transaction
        {
            Reference = TransactionReference.Next(),
            Type = TransactionType.Transfer,
            Status = TransactionStatus.Failed,
            FailureCode = failureCode,
            SenderWalletId = sender.Id,
            ReceiverWalletId = receiver?.Id,
            RequestedReceiver = request.RecipientWalletNumber ?? request.RecipientPhone,
            Amount = request.Amount,
            Fee = 0m,
            Note = request.Note,
            InitiatedByUserId = userId,
            CreatedAt = now
        };

        db.Transactions.Add(transaction);
        db.AuditLogs.Add(AuditEntry.Create(AuditActions.TransferFailed, AuditEntityTypes.Transaction, transaction.Reference, failureCode, userId, info, now));
        return transaction;
    }

    private async Task<Transaction> AddCompletedTransferAsync(
        Guid userId, Wallet sender, Wallet receiver, TransferRequest request, RequestInfo info, decimal fee, DateTime now,
        CancellationToken cancellationToken)
    {
        var accounts = await db.LedgerAccounts.AsNoTracking()
            .Where(account =>
                account.WalletId == sender.Id ||
                account.WalletId == receiver.Id ||
                account.Code == LedgerAccountCodes.FeeRevenue)
            .ToListAsync(cancellationToken);

        var entries = LedgerPostings.ForTransfer(
            accounts.Single(account => account.WalletId == sender.Id).Id,
            accounts.Single(account => account.WalletId == receiver.Id).Id,
            accounts.Single(account => account.Code == LedgerAccountCodes.FeeRevenue).Id,
            request.Amount,
            fee,
            now);
        LedgerPostings.EnsureBalanced(entries);

        // The cached balance moves in the same transaction as the entries, so the two cannot disagree.
        sender.Balance -= request.Amount + fee;
        receiver.Balance += request.Amount;

        var transaction = new Transaction
        {
            Reference = TransactionReference.Next(),
            Type = TransactionType.Transfer,
            Status = TransactionStatus.Completed,
            SenderWalletId = sender.Id,
            ReceiverWalletId = receiver.Id,
            Amount = request.Amount,
            Fee = fee,
            Note = request.Note,
            InitiatedByUserId = userId,
            CreatedAt = now,
            Entries = entries
        };

        db.Transactions.Add(transaction);
        db.AuditLogs.Add(AuditEntry.Create(AuditActions.Transfer, AuditEntityTypes.Transaction, transaction.Reference, null, userId, info, now));
        return transaction;
    }
}
