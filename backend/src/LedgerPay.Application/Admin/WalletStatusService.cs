using LedgerPay.Application.Abstractions;
using LedgerPay.Application.Common;
using LedgerPay.Domain.Constants;
using LedgerPay.Domain.Entities;
using LedgerPay.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LedgerPay.Application.Admin;

public sealed class WalletStatusService(IAppDbContext db, TimeProvider clock) : IWalletStatusService
{
    public Task<ServiceResult<WalletStatusResponse>> SetStatusAsync(
        Guid actorUserId, string walletNumber, WalletStatusRequest request, RequestInfo info, CancellationToken cancellationToken) =>
        db.ExecuteInTransactionAsync(
            token => ChangeAsync(actorUserId, walletNumber, request, info, token),
            cancellationToken);

    private async Task<ServiceResult<WalletStatusResponse>> ChangeAsync(
        Guid actorUserId, string walletNumber, WalletStatusRequest request, RequestInfo info, CancellationToken cancellationToken)
    {
        // The validator checks this first. It is checked again here because a missing status must never read as Active.
        if (request.Status is not { } status)
        {
            return ServiceResult<WalletStatusResponse>.Fail(ErrorCodes.ValidationFailed);
        }

        var walletId = await db.Wallets.AsNoTracking()
            .Where(candidate => candidate.WalletNumber == walletNumber)
            .Select(candidate => (Guid?)candidate.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (walletId is null)
        {
            return ServiceResult<WalletStatusResponse>.Fail(ErrorCodes.WalletNotFound);
        }

        // Locked like a transfer locks it, so a transfer that is already running finishes first
        // and the next one sees the new status.
        var wallet = await db.LockWalletAsync(walletId.Value, cancellationToken)
            ?? throw new InvalidOperationException($"Wallet {walletId} disappeared while its status was changed.");

        if (wallet.Status == status)
        {
            return ServiceResult<WalletStatusResponse>.Fail(ErrorCodes.WalletAlreadyInState);
        }

        var reason = request.Reason.Trim();
        var now = clock.GetUtcNow().UtcDateTime;

        wallet.Status = status;
        wallet.StatusReason = reason;
        wallet.StatusChangedByUserId = actorUserId;
        wallet.StatusChangedAt = now;

        var action = status == WalletStatus.Frozen ? AuditActions.WalletFrozen : AuditActions.WalletUnfrozen;
        db.AuditLogs.Add(AuditEntry.Create(action, AuditEntityTypes.Wallet, wallet.WalletNumber, reason, actorUserId, info, now));

        await db.SaveChangesAsync(cancellationToken);
        return ServiceResult<WalletStatusResponse>.Ok(new WalletStatusResponse(wallet.WalletNumber, wallet.Status, reason, now));
    }
}
