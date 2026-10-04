using LedgerPay.Application.Abstractions;
using LedgerPay.Application.Common;
using LedgerPay.Domain.Constants;
using LedgerPay.Domain.Entities;
using LedgerPay.Domain.Rules;
using Microsoft.EntityFrameworkCore;

namespace LedgerPay.Application.Auth;

public sealed class SessionService(
    IAppDbContext db,
    ITokenService tokens,
    IRefreshTokenService refreshTokens,
    TimeProvider clock) : ISessionService
{
    // A person has a handful of devices. The cap keeps the list a page.
    private const int MaxListed = 50;

    public IssuedRefreshToken Start(Guid userId, DateTime now, RequestInfo info)
    {
        var token = refreshTokens.NewToken();
        var row = NewRow(userId, Guid.NewGuid(), token, now, now, info);
        db.RefreshTokens.Add(row);
        return new IssuedRefreshToken(token, row.ExpiresAt);
    }

    public async Task<ServiceResult<Refreshed>> RefreshAsync(string? token, RequestInfo info, CancellationToken cancellationToken)
    {
        // Every way of being refused gives the same answer, so a caller learns nothing about which part was wrong.
        if (!SessionRules.IsWellFormed(token))
        {
            return ServiceResult<Refreshed>.Fail(ErrorCodes.InvalidRefreshToken);
        }

        var hash = refreshTokens.Hash(token!);
        var familyId = await FamilyOfAsync(hash, cancellationToken);
        if (familyId is null)
        {
            return ServiceResult<Refreshed>.Fail(ErrorCodes.InvalidRefreshToken);
        }

        return await db.ExecuteInTransactionAsync(
            work => RefreshOnceAsync(hash, familyId.Value, info, work),
            cancellationToken);
    }

    public async Task EndAsync(string? token, RequestInfo info, CancellationToken cancellationToken)
    {
        if (!SessionRules.IsWellFormed(token))
        {
            return;
        }

        var hash = refreshTokens.Hash(token!);
        var familyId = await FamilyOfAsync(hash, cancellationToken);
        if (familyId is null)
        {
            return;
        }

        await db.ExecuteInTransactionAsync(async work =>
        {
            await db.LockResourceAsync(LockName(familyId.Value), work);
            var now = clock.GetUtcNow().UtcDateTime;
            var live = await LiveRowsAsync(familyId.Value, null, work);
            if (live.Count > 0)
            {
                Revoke(live, now);
                db.AuditLogs.Add(AuditEntry.Create(
                    AuditActions.Logout, AuditEntityTypes.Session, familyId.Value.ToString(), null, live[0].UserId, info, now));
                await db.SaveChangesAsync(work);
            }

            return 0;
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<SessionResponse>> ListAsync(Guid userId, string? currentToken, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var current = await CurrentFamilyAsync(userId, currentToken, cancellationToken);

        // The newest unrevoked row of a family is the one the browser holds, so one row is one session.
        var rows = await db.RefreshTokens.AsNoTracking()
            .Where(row => row.UserId == userId && row.RevokedAt == null && row.ExpiresAt > now)
            .OrderByDescending(row => row.CreatedAt)
            .Take(MaxListed)
            .Select(row => new { row.FamilyId, row.SessionStartedAt, row.CreatedAt, row.IpAddress, row.UserAgent })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new SessionResponse(
                row.FamilyId, row.SessionStartedAt, row.CreatedAt, row.IpAddress, row.UserAgent, row.FamilyId == current))
            .ToList();
    }

    public async Task<ServiceResult<bool>> RevokeAsync(
        Guid userId, Guid sessionId, string? currentToken, RequestInfo info, CancellationToken cancellationToken)
    {
        var current = await CurrentFamilyAsync(userId, currentToken, cancellationToken);

        return await db.ExecuteInTransactionAsync(async work =>
        {
            await db.LockResourceAsync(LockName(sessionId), work);
            var now = clock.GetUtcNow().UtcDateTime;

            // The user id is part of the filter, so a session of someone else is the same as one that does not exist.
            var live = await LiveRowsAsync(sessionId, userId, work);
            if (live.Count == 0)
            {
                return ServiceResult<bool>.Fail(ErrorCodes.SessionNotFound);
            }

            Revoke(live, now);
            db.AuditLogs.Add(AuditEntry.Create(
                AuditActions.SessionRevoked, AuditEntityTypes.Session, sessionId.ToString(), null, userId, info, now));
            await db.SaveChangesAsync(work);
            return ServiceResult<bool>.Ok(current == sessionId);
        }, cancellationToken);
    }

    private async Task<ServiceResult<Refreshed>> RefreshOnceAsync(
        string hash, Guid familyId, RequestInfo info, CancellationToken cancellationToken)
    {
        // Two requests with the same token take turns, so one of them rotates it and the other sees what happened.
        await db.LockResourceAsync(LockName(familyId), cancellationToken);
        var now = clock.GetUtcNow().UtcDateTime;

        var presented = await db.RefreshTokens.SingleOrDefaultAsync(row => row.TokenHash == hash, cancellationToken);
        if (presented is null)
        {
            return ServiceResult<Refreshed>.Fail(ErrorCodes.InvalidRefreshToken);
        }

        if (presented.RevokedAt is null)
        {
            return presented.ExpiresAt <= now
                ? ServiceResult<Refreshed>.Fail(ErrorCodes.InvalidRefreshToken)
                : await RotateAsync(presented, now, info, cancellationToken);
        }

        // A token that was replaced and comes back is either a request that crossed another one, or a copy.
        if (presented.ReplacedById is not null)
        {
            var successor = await db.RefreshTokens.AsNoTracking()
                .SingleOrDefaultAsync(row => row.Id == presented.ReplacedById, cancellationToken);

            if (successor is { RevokedAt: null } && successor.ExpiresAt > now && now - presented.RevokedAt.Value <= SessionRules.ReuseGrace)
            {
                var owner = await db.Users.SingleAsync(user => user.Id == presented.UserId, cancellationToken);
                var response = await LoginResponseBuilder.BuildAsync(db, tokens, owner, now, cancellationToken);

                // Left on record with the address, because this is also what a copy of a token used just in time looks like.
                db.AuditLogs.Add(AuditEntry.Create(
                    AuditActions.RefreshGraceUsed, AuditEntityTypes.Session, presented.FamilyId.ToString(), null, owner.Id, info, now));
                await db.SaveChangesAsync(cancellationToken);
                return ServiceResult<Refreshed>.Ok(new Refreshed(response, null));
            }

            await RevokeFamilyAfterReuseAsync(presented, now, info, cancellationToken);
        }

        return ServiceResult<Refreshed>.Fail(ErrorCodes.InvalidRefreshToken);
    }

    private async Task<ServiceResult<Refreshed>> RotateAsync(
        RefreshToken presented, DateTime now, RequestInfo info, CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleAsync(row => row.Id == presented.UserId, cancellationToken);

        // The restriction already ended every session. This is for one that was started in the same moment.
        if (user.RestrictedAt is not null)
        {
            presented.RevokedAt = now;
            await db.SaveChangesAsync(cancellationToken);
            return ServiceResult<Refreshed>.Fail(ErrorCodes.InvalidRefreshToken);
        }

        var token = refreshTokens.NewToken();
        var next = NewRow(user.Id, presented.FamilyId, token, presented.SessionStartedAt, now, info);
        presented.RevokedAt = now;

        // The key is made when the row is added, as for every other table, so it is sequential and the index stays tidy.
        db.RefreshTokens.Add(next);
        presented.ReplacedById = next.Id;
        db.AuditLogs.Add(AuditEntry.Create(
            AuditActions.RefreshRotated, AuditEntityTypes.Session, presented.FamilyId.ToString(), null, user.Id, info, now));

        var response = await LoginResponseBuilder.BuildAsync(db, tokens, user, now, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ServiceResult<Refreshed>.Ok(new Refreshed(response, new IssuedRefreshToken(token, next.ExpiresAt)));
    }

    private async Task RevokeFamilyAfterReuseAsync(
        RefreshToken presented, DateTime now, RequestInfo info, CancellationToken cancellationToken)
    {
        var live = await LiveRowsAsync(presented.FamilyId, null, cancellationToken);

        // A family that is already revoked has nothing left to end, so a second try with the old token adds no entry.
        if (live.Count == 0)
        {
            return;
        }

        Revoke(live, now);
        db.AuditLogs.Add(AuditEntry.Create(
            AuditActions.RefreshReuseDetected, AuditEntityTypes.Session, presented.FamilyId.ToString(), null, presented.UserId, info, now));
        await db.SaveChangesAsync(cancellationToken);
    }

    private RefreshToken NewRow(Guid userId, Guid familyId, string token, DateTime sessionStartedAt, DateTime now, RequestInfo info) => new()
    {
        UserId = userId,
        FamilyId = familyId,
        TokenHash = refreshTokens.Hash(token),
        CreatedAt = now,
        SessionStartedAt = sessionStartedAt,
        ExpiresAt = SessionRules.ExpiresAt(now, sessionStartedAt),
        IpAddress = info.IpAddress,
        UserAgent = info.UserAgent
    };

    private static void Revoke(List<RefreshToken> rows, DateTime now)
    {
        foreach (var row in rows)
        {
            row.RevokedAt = now;
        }
    }

    // The rows of a session that can still be used. With a user id, only if that user owns it.
    private Task<List<RefreshToken>> LiveRowsAsync(Guid familyId, Guid? userId, CancellationToken cancellationToken) =>
        db.RefreshTokens
            .Where(row => row.FamilyId == familyId && row.RevokedAt == null && (userId == null || row.UserId == userId))
            .ToListAsync(cancellationToken);

    private async Task<Guid?> FamilyOfAsync(string hash, CancellationToken cancellationToken) =>
        await db.RefreshTokens.AsNoTracking()
            .Where(row => row.TokenHash == hash)
            .Select(row => (Guid?)row.FamilyId)
            .SingleOrDefaultAsync(cancellationToken);

    // The session the caller is using, found from the cookie. A cookie that is missing or belongs to someone else gives none.
    private async Task<Guid?> CurrentFamilyAsync(Guid userId, string? token, CancellationToken cancellationToken)
    {
        if (!SessionRules.IsWellFormed(token))
        {
            return null;
        }

        var hash = refreshTokens.Hash(token!);
        return await db.RefreshTokens.AsNoTracking()
            .Where(row => row.TokenHash == hash && row.UserId == userId)
            .Select(row => (Guid?)row.FamilyId)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static string LockName(Guid familyId) => "refresh-family:" + familyId;
}
