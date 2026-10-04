using LedgerPay.Application.Abstractions;
using LedgerPay.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LedgerPay.Application.Auth;

// What a sign-in and a refresh both answer with: a new access token and who it is for.
internal static class LoginResponseBuilder
{
    public static async Task<LoginResponse> BuildAsync(
        IAppDbContext db, ITokenService tokens, User user, DateTime now, CancellationToken cancellationToken)
    {
        var roles = await db.UserRoles.AsNoTracking()
            .Where(userRole => userRole.UserId == user.Id)
            .Select(userRole => userRole.Role.Name)
            .ToListAsync(cancellationToken);
        var walletNumber = await db.Wallets.AsNoTracking()
            .Where(wallet => wallet.UserId == user.Id)
            .Select(wallet => wallet.WalletNumber)
            .SingleOrDefaultAsync(cancellationToken);

        var accessToken = tokens.Create(user.Id, roles, now);
        return new LoginResponse(accessToken.Token, "Bearer", accessToken.ExpiresAt, user.FullName, roles, walletNumber);
    }
}
