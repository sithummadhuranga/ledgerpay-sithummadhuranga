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

namespace LedgerPay.Application.TopUps;

public sealed class TopUpService(
    IAppDbContext db,
    LedgerSettingsProvider settingsProvider,
    IdempotencyService idempotency,
    TimeProvider clock) : ITopUpService
{
    public Task<ServiceResult<TopUpResponse>> TopUpAsync(
        Guid operatorUserId, string? idempotencyKey, TopUpRequest request, RequestInfo info, CancellationToken cancellationToken)
    {
        var keyError = IdempotencyService.ValidateKey(idempotencyKey);
        if (keyError is not null)
        {
            return Task.FromResult(ServiceResult<TopUpResponse>.Fail(keyError));
        }

        // Bank references are kept in upper case, so abc123456 and ABC123456 are the same reference.
        var normalized = request with { BankReference = request.BankReference.ToUpperInvariant() };

        return idempotency.RunAsync(
            operatorUserId,
            idempotencyKey,
            IdempotencyEndpoints.TopUps,
            RequestHasher.Hash(normalized),
            (record, token) => PostTopUpAsync(operatorUserId, normalized, info, record, token),
            cancellationToken);
    }

    // Runs inside the transaction the idempotency service opened, after it inserted the key.
    // Nothing is saved here: the idempotency service saves once at the end, with the stored answer.
    private async Task<ServiceResult<TopUpResponse>> PostTopUpAsync(
        Guid operatorUserId, TopUpRequest request, RequestInfo info, IdempotencyKey record, CancellationToken cancellationToken)
    {
        var settings = await settingsProvider.GetAsync(cancellationToken);

        // One top-up per bank reference at a time. Without this, two operators could both pass the duplicate
        // check below and the unique index would only catch the second one when it was too late to answer well.
        await db.LockResourceAsync("bank-reference:" + request.BankReference, cancellationToken);

        var walletId = await db.Wallets.AsNoTracking()
            .Where(candidate => candidate.WalletNumber == request.WalletNumber)
            .Select(candidate => (Guid?)candidate.Id)
            .SingleOrDefaultAsync(cancellationToken);
        var wallet = walletId is null ? null : await db.LockWalletAsync(walletId.Value, cancellationToken);

        var bankReferenceUsed = await db.Transactions.AsNoTracking().AnyAsync(
            transaction => transaction.Type == TransactionType.TopUp &&
                           transaction.Status == TransactionStatus.Completed &&
                           transaction.BankReference == request.BankReference,
            cancellationToken);

        var now = clock.GetUtcNow().UtcDateTime;
        var failure = TopUpRules.FirstFailure(wallet, bankReferenceUsed, request.Amount, settings);
        if (failure is not null)
        {
            var failed = AddFailedTopUp(operatorUserId, wallet, request, info, failure, now);
            record.TransactionId = failed.Id;
            return ServiceResult<TopUpResponse>.Fail(failure);
        }

        var completed = await AddCompletedTopUpAsync(operatorUserId, wallet!, request, info, now, cancellationToken);
        record.TransactionId = completed.Id;

        return ServiceResult<TopUpResponse>.Ok(new TopUpResponse(
            completed.Reference, wallet!.WalletNumber, request.Amount, wallet.Balance, request.BankReference, now));
    }

    private Transaction AddFailedTopUp(
        Guid operatorUserId, Wallet? wallet, TopUpRequest request, RequestInfo info, string failureCode, DateTime now)
    {
        var transaction = new Transaction
        {
            Reference = TransactionReference.Next(),
            Type = TransactionType.TopUp,
            Status = TransactionStatus.Failed,
            FailureCode = failureCode,
            ReceiverWalletId = wallet?.Id,
            RequestedReceiver = request.WalletNumber,
            Amount = request.Amount,
            Fee = 0m,
            Note = request.Note,
            BankReference = request.BankReference,
            InitiatedByUserId = operatorUserId,
            CreatedAt = now
        };

        db.Transactions.Add(transaction);
        db.AuditLogs.Add(AuditEntry.Create(AuditActions.TopUpFailed, AuditEntityTypes.Transaction, transaction.Reference, failureCode, operatorUserId, info, now));
        return transaction;
    }

    private async Task<Transaction> AddCompletedTopUpAsync(
        Guid operatorUserId, Wallet wallet, TopUpRequest request, RequestInfo info, DateTime now, CancellationToken cancellationToken)
    {
        var accounts = await db.LedgerAccounts.AsNoTracking()
            .Where(account => account.WalletId == wallet.Id || account.Code == LedgerAccountCodes.SettlementFloat)
            .ToListAsync(cancellationToken);

        var entries = LedgerPostings.ForTopUp(
            accounts.Single(account => account.Code == LedgerAccountCodes.SettlementFloat).Id,
            accounts.Single(account => account.WalletId == wallet.Id).Id,
            request.Amount,
            now);
        LedgerPostings.EnsureBalanced(entries);

        wallet.Balance += request.Amount;

        var transaction = new Transaction
        {
            Reference = TransactionReference.Next(),
            Type = TransactionType.TopUp,
            Status = TransactionStatus.Completed,
            ReceiverWalletId = wallet.Id,
            Amount = request.Amount,
            Fee = 0m,
            Note = request.Note,
            BankReference = request.BankReference,
            InitiatedByUserId = operatorUserId,
            CreatedAt = now,
            Entries = entries
        };

        db.Transactions.Add(transaction);
        db.AuditLogs.Add(AuditEntry.Create(AuditActions.TopUp, AuditEntityTypes.Transaction, transaction.Reference, null, operatorUserId, info, now));
        return transaction;
    }
}
