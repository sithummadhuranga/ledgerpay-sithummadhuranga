using LedgerPay.Api.Errors;
using LedgerPay.Domain.Constants;
using LedgerPay.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace LedgerPay.Api.Extensions;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, JwtOptions jwt)
    {
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Keep the claim names as they are in the token, so the names in JwtClaimNames are what the code reads.
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(jwt.SigningKeyBytes()),
                    RequireSignedTokens = true,

                    // Only HS256, so a token cannot pick a weaker algorithm for itself, and "none" is refused.
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],

                    // The default five minutes of slack would make a 15 minute token last 20.
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = JwtClaimNames.Subject,
                    RoleClaimType = JwtClaimNames.Role
                };

                options.Events = new JwtBearerEvents
                {
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.Headers.WWWAuthenticate = "Bearer";
                        await Problems.WriteAsync(context.HttpContext, ErrorCodes.Unauthenticated);
                    },
                    OnForbidden = context => Problems.WriteAsync(context.HttpContext, ErrorCodes.Forbidden)
                };
            });

        services.AddAuthorization();
        return services;
    }
}
