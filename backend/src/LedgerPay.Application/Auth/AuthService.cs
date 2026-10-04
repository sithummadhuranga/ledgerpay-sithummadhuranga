using LedgerPay.Application.Abstractions;
using LedgerPay.Application.Common;
using LedgerPay.Domain.Constants;
using LedgerPay.Domain.Entities;
using LedgerPay.Domain.Rules;
using Microsoft.EntityFrameworkCore;

namespace LedgerPay.Application.Auth;

public sealed class AuthService(
    IAppDbContext db,
    IPasswordService passwords,
    ITokenService tokens,
    ISessionService sessions,
    TimeProvider clock) : IAuthService
{
    // The wallet number is random. Two registrations can pick the same one, and the unique index then
    // refuses the second, which is tried again with a new number.
    private const int WalletNumberAttempts = 3;

    public async Task<ServiceResult<RegisterResponse>> RegisterAsync(
        RegisterRequest request, RequestInfo info, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var fullName = request.FullName.Trim();

        // Hashed once, before any transaction, because hashing is slow on purpose.
        var passwordHash = passwords.Hash(request.Password);

        for (var attempt = 0; attempt < WalletNumberAttempts; attempt++)
        {
            try
            {
                return await db.ExecuteInTransactionAsync(
                    token => RegisterOnceAsync(fullName, email, request.Phone, passwordHash, info, token),
                    cancellationToken);
            }
            catch (DbUpdateException error) when (db.IsDuplicateKey(error))
            {
                // Two requests with the same email or phone can both pass the check and meet at the unique index.
                var taken = await FindTakenAsync(email, request.Phone, cancellationToken);
                if (taken is not null)
                {
                    return ServiceResult<RegisterResponse>.Fail(taken);
                }

                // Neither is taken, so it was the wallet number. Go round again with a new one.
            }
        }

        throw new InvalidOperationException("Could not find a free wallet number.");
    }

    public async Task<ServiceResult<SignedIn>> LoginAsync(
        LoginRequest request, RequestInfo info, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var userId = await db.Users.AsNoTracking()
            .Where(user => user.Email == email)
            .Select(user => (Guid?)user.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (userId is null)
        {
            // The same work as for a known account, so the response time does not give the email away.
            passwords.VerifyUnknownAccount(request.Password);
            return await RecordUnknownAccountAsync(info, cancellationToken);
        }

        return await db.ExecuteInTransactionAsync(
            token => SignInAsync(userId.Value, request.Password, info, token),
            cancellationToken);
    }

    private async Task<ServiceResult<RegisterResponse>> RegisterOnceAsync(
        string fullName, string email, string phone, string passwordHash, RequestInfo info, CancellationToken cancellationToken)
    {
        var taken = await FindTakenAsync(email, phone, cancellationToken);
        if (taken is not null)
        {
            return ServiceResult<RegisterResponse>.Fail(taken);
        }

        var customerRole = await db.Roles.SingleAsync(role => role.Name == RoleNames.Customer, cancellationToken);
        var now = clock.GetUtcNow().UtcDateTime;

        var user = new User
        {
            Email = email,
            Phone = phone,
            FullName = fullName,
            PasswordHash = passwordHash,
            CreatedAt = now
        };
        user.UserRoles.Add(new UserRole { User = user, Role = customerRole });

        var wallet = Wallet.Open(user, now);
        user.Wallet = wallet;

        db.Users.Add(user);
        db.LedgerAccounts.Add(LedgerAccount.ForWallet(wallet));
        db.AuditLogs.Add(AuditEntry.Create(
            AuditActions.Register, AuditEntityTypes.User, wallet.WalletNumber, null, user.Id, info, now));

        await db.SaveChangesAsync(cancellationToken);
        return ServiceResult<RegisterResponse>.Ok(new RegisterResponse(fullName, email, phone, wallet.WalletNumber));
    }

    // The email is reported before the phone when both are taken.
    private async Task<string?> FindTakenAsync(string email, string phone, CancellationToken cancellationToken)
    {
        if (await db.Users.AsNoTracking().AnyAsync(user => user.Email == email, cancellationToken))
        {
            return ErrorCodes.EmailAlreadyRegistered;
        }

        if (await db.Users.AsNoTracking().AnyAsync(user => user.Phone == phone, cancellationToken))
        {
            return ErrorCodes.PhoneAlreadyRegistered;
        }

        return null;
    }

    private async Task<ServiceResult<SignedIn>> SignInAsync(
        Guid userId, string password, RequestInfo info, CancellationToken cancellationToken)
    {
        // The row is locked, so parallel guesses for one account take turns and each one sees the count before it.
        var user = await db.LockUserAsync(userId, cancellationToken)
            ?? throw new InvalidOperationException($"User {userId} disappeared during sign-in.");
        var now = clock.GetUtcNow().UtcDateTime;

        // A lock that has run out is cleared first, so the count starts again from zero.
        if (user.LockoutEnd is not null && user.LockoutEnd <= now)
        {
            user.LockoutEnd = null;
            user.FailedLoginCount = 0;
        }

        // Checked before the password, so a locked account says the same thing whether the password was right or not.
        if (user.LockoutEnd is not null)
        {
            Audit(AuditActions.LoginFailed, user.Id, "Account is locked", info, now);
            await db.SaveChangesAsync(cancellationToken);
            var secondsLeft = (int)Math.Ceiling((user.LockoutEnd.Value - now).TotalSeconds);
            return ServiceResult<SignedIn>.FailAndWait(ErrorCodes.AccountLocked, secondsLeft);
        }

        if (!passwords.Verify(user.PasswordHash, password))
        {
            return await RecordWrongPasswordAsync(user, info, now, cancellationToken);
        }

        user.FailedLoginCount = 0;
        user.LockoutEnd = null;

        // Said only to someone who knew the password, so a guesser learns nothing about the account.
        if (user.RestrictedAt is not null)
        {
            Audit(AuditActions.LoginFailed, user.Id, "Account is restricted", info, now);
            await db.SaveChangesAsync(cancellationToken);
            return ServiceResult<SignedIn>.Fail(ErrorCodes.AccountRestricted);
        }

        var response = await LoginResponseBuilder.BuildAsync(db, tokens, user, now, cancellationToken);

        // The session is saved with the sign-in, so there is never a sign-in the user could not come back to.
        var refresh = sessions.Start(user.Id, now, info);
        Audit(AuditActions.LoginSucceeded, user.Id, null, info, now);
        await db.SaveChangesAsync(cancellationToken);

        return ServiceResult<SignedIn>.Ok(new SignedIn(response, refresh));
    }

    private async Task<ServiceResult<SignedIn>> RecordWrongPasswordAsync(
        User user, RequestInfo info, DateTime now, CancellationToken cancellationToken)
    {
        user.FailedLoginCount++;

        if (user.FailedLoginCount >= LoginLockout.MaxFailedAttempts)
        {
            user.LockoutEnd = now + LoginLockout.Duration;
            Audit(AuditActions.AccountLocked, user.Id, null, info, now);
            await db.SaveChangesAsync(cancellationToken);
            return ServiceResult<SignedIn>.FailAndWait(ErrorCodes.AccountLocked, (int)LoginLockout.Duration.TotalSeconds);
        }

        Audit(AuditActions.LoginFailed, user.Id, null, info, now);
        await db.SaveChangesAsync(cancellationToken);
        return ServiceResult<SignedIn>.Fail(ErrorCodes.InvalidCredentials);
    }

    private async Task<ServiceResult<SignedIn>> RecordUnknownAccountAsync(RequestInfo info, CancellationToken cancellationToken)
    {
        // No email in the entry: it is personal data and belongs to nobody we know.
        Audit(AuditActions.LoginFailed, null, "Unknown account", info, clock.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(cancellationToken);
        return ServiceResult<SignedIn>.Fail(ErrorCodes.InvalidCredentials);
    }

    private void Audit(string action, Guid? userId, string? details, RequestInfo info, DateTime now) =>
        db.AuditLogs.Add(new AuditLog
        {
            CreatedAt = now,
            ActorUserId = userId,
            Action = action,
            EntityType = AuditEntityTypes.User,
            IpAddress = info.IpAddress,
            CorrelationId = info.CorrelationId,
            Details = details
        });
}
