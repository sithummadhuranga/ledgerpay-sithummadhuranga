using System.Security.Claims;
using LedgerPay.Infrastructure.Security;

namespace LedgerPay.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    // The id of the signed-in user, read from the token. It is the only place a user id may come from.
    public static Guid? UserId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(JwtClaimNames.Subject), out var id) ? id : null;
}
