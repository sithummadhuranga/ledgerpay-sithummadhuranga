using LedgerPay.Application.Abstractions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace LedgerPay.Infrastructure.Security;

public sealed class JwtTokenService(JwtOptions options) : ITokenService
{
    private readonly JsonWebTokenHandler handler = new();

    public AccessToken Create(Guid userId, IReadOnlyCollection<string> roles, DateTime now)
    {
        var expiresAt = now.AddMinutes(options.AccessTokenMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = options.Issuer,
            Audience = options.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expiresAt,
            Claims = new Dictionary<string, object>
            {
                [JwtClaimNames.Subject] = userId.ToString(),
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
                [JwtClaimNames.Role] = roles.ToArray()
            },
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(options.SigningKeyBytes()), SecurityAlgorithms.HmacSha256)
        };

        return new AccessToken(handler.CreateToken(descriptor), expiresAt);
    }
}
