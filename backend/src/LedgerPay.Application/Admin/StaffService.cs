using LedgerPay.Application.Abstractions;
using LedgerPay.Application.Common;
using LedgerPay.Domain.Constants;
using LedgerPay.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LedgerPay.Application.Admin;

public sealed class StaffService(IAppDbContext db, TimeProvider clock) : IStaffService
{
    public async Task<IReadOnlyList<StaffMember>> ListAsync(CancellationToken cancellationToken)
    {
        // Operators and admins are the users who hold either role. A customer is never in this list.
        var rows = await db.Users.AsNoTracking()
            .Where(user => user.UserRoles.Any(userRole => userRole.Role.Name == RoleNames.Operator || userRole.Role.Name == RoleNames.Admin))
            .OrderBy(user => user.FullName)
            .Select(user => new
            {
                user.FullName, user.Email, IsAdmin = user.UserRoles.Any(userRole => userRole.Role.Name == RoleNames.Admin),
                user.RestrictedAt, user.RestrictedReason, user.RestrictedByUserId, user.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var names = await NamesAsync(rows.Select(row => row.RestrictedByUserId).OfType<Guid>().Distinct().ToList(), cancellationToken);
        return rows
            .Select(row => new StaffMember(
                row.FullName, row.Email, row.IsAdmin ? RoleNames.Admin : RoleNames.Operator, row.RestrictedAt is not null,
                row.RestrictedAt is { } at ? Utc(at) : null, row.RestrictedReason,
                row.RestrictedByUserId is { } by && names.TryGetValue(by, out var name) ? name : null, Utc(row.CreatedAt)))
            .ToList();
    }

    public Task<ServiceResult<StaffMember>> SetRestrictionAsync(
        Guid actorUserId, StaffRestrictionRequest request, RequestInfo info, CancellationToken cancellationToken)
    {
        // A request without the flag would read as false and lift a restriction, so it is refused here as well as by the validator.
        if (request.Restricted is not { } restrict)
        {
            return Task.FromResult(ServiceResult<StaffMember>.Fail(ErrorCodes.ValidationFailed));
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var reason = request.Reason!.Trim();
        return db.ExecuteInTransactionAsync(token => SetOnceAsync(actorUserId, email, restrict, reason, info, token), cancellationToken);
    }

    private async Task<ServiceResult<StaffMember>> SetOnceAsync(
        Guid actorUserId, string email, bool restrict, string reason, RequestInfo info, CancellationToken cancellationToken)
    {
        var userId = await db.Users.AsNoTracking().Where(user => user.Email == email).Select(user => (Guid?)user.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (userId is null)
        {
            return ServiceResult<StaffMember>.Fail(ErrorCodes.StaffNotFound);
        }

        // The row is locked, so two admins acting on one account take turns and each sees what the other did.
        var user = await db.LockUserAsync(userId.Value, cancellationToken) ?? throw new InvalidOperationException("The user disappeared.");
        var roles = await db.UserRoles.AsNoTracking().Where(userRole => userRole.UserId == user.Id).Select(userRole => userRole.Role.Name)
            .ToListAsync(cancellationToken);

        // A customer is not staff, so to this screen they do not exist. An admin can not be restricted, and nobody can restrict themselves.
        if (!roles.Contains(RoleNames.Operator) && !roles.Contains(RoleNames.Admin))
        {
            return ServiceResult<StaffMember>.Fail(ErrorCodes.StaffNotFound);
        }

        if (roles.Contains(RoleNames.Admin) || user.Id == actorUserId)
        {
            return ServiceResult<StaffMember>.Fail(ErrorCodes.StaffNotRestrictable);
        }

        if ((user.RestrictedAt is not null) == restrict)
        {
            return ServiceResult<StaffMember>.Fail(ErrorCodes.AccountAlreadyInState);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        if (restrict)
        {
            user.RestrictedAt = now;
            user.RestrictedReason = reason;
            user.RestrictedByUserId = actorUserId;

            // Every session ends now. The access token already issued stops working at its next use, because the API asks.
            var live = await db.RefreshTokens.Where(row => row.UserId == user.Id && row.RevokedAt == null).ToListAsync(cancellationToken);
            foreach (var row in live)
            {
                row.RevokedAt = now;
            }
        }
        else
        {
            user.RestrictedAt = null;
            user.RestrictedReason = null;
            user.RestrictedByUserId = null;
        }

        db.AuditLogs.Add(AuditEntry.Create(
            restrict ? AuditActions.AccountRestricted : AuditActions.AccountRestrictionLifted,
            AuditEntityTypes.User, user.Id.ToString(), $"{user.FullName}: {reason}", actorUserId, info, now));
        await db.SaveChangesAsync(cancellationToken);

        var actorName = restrict
            ? (await NamesAsync([actorUserId], cancellationToken)).GetValueOrDefault(actorUserId)
            : null;
        return ServiceResult<StaffMember>.Ok(new StaffMember(
            user.FullName, user.Email, RoleNames.Operator, restrict, restrict ? now : null, restrict ? reason : null, actorName, DateTime.SpecifyKind(user.CreatedAt, DateTimeKind.Utc)));
    }

    private async Task<Dictionary<Guid, string>> NamesAsync(List<Guid> ids, CancellationToken cancellationToken) =>
        ids.Count == 0
            ? []
            : await db.Users.AsNoTracking().Where(user => ids.Contains(user.Id)).ToDictionaryAsync(user => user.Id, user => user.FullName, cancellationToken);

    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
