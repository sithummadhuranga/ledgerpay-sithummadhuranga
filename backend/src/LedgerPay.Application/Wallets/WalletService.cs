using LedgerPay.Application.Abstractions;
using LedgerPay.Application.Common;
using LedgerPay.Domain.Constants;
using LedgerPay.Domain.Enums;
using LedgerPay.Domain.Rules;
using Microsoft.EntityFrameworkCore;

namespace LedgerPay.Application.Wallets;

public sealed class WalletService(IAppDbContext db) : IWalletService
{
    private const string Currency = "LKR";

    public async Task<ServiceResult<WalletResponse>> GetMineAsync(Guid userId, CancellationToken cancellationToken)
    {
        // StatusReason is never selected: the reason for a freeze is for the back office only.
        var wallet = await db.Wallets.AsNoTracking()
            .Where(candidate => candidate.UserId == userId)
            .Select(candidate => new { candidate.WalletNumber, candidate.User.FullName, candidate.Balance, candidate.Status })
            .SingleOrDefaultAsync(cancellationToken);
        if (wallet is null)
        {
            return ServiceResult<WalletResponse>.Fail(ErrorCodes.WalletNotFound);
        }

        // There are no holds at this level, so the available balance is the balance. AvailableBalance keeps that rule in one place.
        return ServiceResult<WalletResponse>.Ok(new WalletResponse(
            wallet.WalletNumber, wallet.FullName, wallet.Balance, wallet.Balance, Currency, wallet.Status));
    }

    public async Task<ServiceResult<LookupResponse>> LookupAsync(LookupQuery query, CancellationToken cancellationToken)
    {
        // The validator checks this first. It is checked again because a lookup with neither value would match any wallet whose phone is null.
        if ((query.WalletNumber is null) == (query.Phone is null))
        {
            return ServiceResult<LookupResponse>.Fail(ErrorCodes.ValidationFailed);
        }

        var wallets = db.Wallets.AsNoTracking();
        var matches = query.WalletNumber is not null
            ? wallets.Where(candidate => candidate.WalletNumber == query.WalletNumber)
            : wallets.Where(candidate => candidate.User.Phone == query.Phone);

        var wallet = await matches
            .Select(candidate => new { candidate.WalletNumber, candidate.User.FullName, candidate.Status })
            .SingleOrDefaultAsync(cancellationToken);
        if (wallet is null)
        {
            return ServiceResult<LookupResponse>.Fail(ErrorCodes.WalletNotFound);
        }

        return ServiceResult<LookupResponse>.Ok(new LookupResponse(
            wallet.WalletNumber, NameMask.Of(wallet.FullName), wallet.Status == WalletStatus.Active));
    }
}
