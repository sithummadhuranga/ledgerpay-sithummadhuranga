using LedgerPay.Infrastructure.Security;
using LedgerPay.IntegrationTests.Support;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace LedgerPay.IntegrationTests.Auth;

public class JwtTokenServiceTests
{
    private static readonly DateTime Now = new(2026, 10, 4, 9, 0, 0, DateTimeKind.Utc);

    private static TokenValidationParameters Parameters(JwtOptions options, bool checkLifetime = true) => new()
    {
        ValidIssuer = options.Issuer,
        ValidAudience = options.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(options.SigningKeyBytes()),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        ValidateLifetime = checkLifetime,
        ClockSkew = TimeSpan.Zero
    };

    [Fact]
    public void The_token_expires_fifteen_minutes_after_it_is_made()
    {
        var service = new JwtTokenService(TestJwt.Options());

        var token = service.Create(Guid.NewGuid(), ["Customer"], DateTime.UtcNow);

        Assert.InRange((token.ExpiresAt - DateTime.UtcNow).TotalMinutes, 14.9, 15.0);
        Assert.Equal(DateTimeKind.Utc, token.ExpiresAt.Kind);
    }

    [Fact]
    public async Task The_token_carries_the_user_id_the_roles_and_a_unique_id_and_validates()
    {
        var options = TestJwt.Options();
        var service = new JwtTokenService(options);
        var userId = Guid.NewGuid();

        var token = service.Create(userId, ["Operator", "Admin"], DateTime.UtcNow);
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token.Token, Parameters(options));

        Assert.True(result.IsValid);
        var jwt = (JsonWebToken)result.SecurityToken;
        Assert.Equal(userId.ToString(), jwt.Subject);
        Assert.Equal(["Admin", "Operator"], jwt.Claims.Where(claim => claim.Type == "role").Select(claim => claim.Value).Order());
        Assert.Equal(options.Issuer, jwt.Issuer);
        Assert.Contains(options.Audience, jwt.Audiences);
        Assert.False(string.IsNullOrEmpty(jwt.Id));
    }

    [Fact]
    public void Every_token_gets_its_own_id()
    {
        var service = new JwtTokenService(TestJwt.Options());
        var handler = new JsonWebTokenHandler();

        var first = handler.ReadJsonWebToken(service.Create(Guid.NewGuid(), ["Customer"], Now).Token);
        var second = handler.ReadJsonWebToken(service.Create(Guid.NewGuid(), ["Customer"], Now).Token);

        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public void The_token_is_signed_with_hmac_sha256()
    {
        var service = new JwtTokenService(TestJwt.Options());

        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(service.Create(Guid.NewGuid(), ["Customer"], Now).Token);

        Assert.Equal(SecurityAlgorithms.HmacSha256, jwt.Alg);
    }

    [Fact]
    public async Task A_token_signed_with_another_key_is_refused()
    {
        var options = TestJwt.Options();
        var other = new JwtTokenService(TestJwt.Options());

        var token = other.Create(Guid.NewGuid(), ["Customer"], DateTime.UtcNow);
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token.Token, Parameters(options));

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task A_token_with_a_changed_payload_is_refused()
    {
        var options = TestJwt.Options();
        var token = new JwtTokenService(options).Create(Guid.NewGuid(), ["Customer"], DateTime.UtcNow).Token;
        var parts = token.Split('.');
        var forgedPayload = Base64UrlEncoder.Encode(System.Text.Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(parts[1])).Replace("Customer", "Admin"));

        var result = await new JsonWebTokenHandler().ValidateTokenAsync($"{parts[0]}.{forgedPayload}.{parts[2]}", Parameters(options));

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task A_token_is_refused_after_it_has_expired()
    {
        var options = TestJwt.Options();
        var token = new JwtTokenService(options).Create(Guid.NewGuid(), ["Customer"], DateTime.UtcNow.AddMinutes(-20));

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token.Token, Parameters(options));

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task A_token_for_another_audience_is_refused()
    {
        var options = TestJwt.Options();
        var token = new JwtTokenService(options).Create(Guid.NewGuid(), ["Customer"], DateTime.UtcNow);
        var strict = Parameters(options);
        strict.ValidAudience = "someone-else";

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token.Token, strict);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task An_unsigned_token_is_refused()
    {
        var options = TestJwt.Options();
        var header = Base64UrlEncoder.Encode("{\"alg\":\"none\",\"typ\":\"JWT\"}");
        var payload = Base64UrlEncoder.Encode($"{{\"sub\":\"{Guid.NewGuid()}\",\"role\":\"Admin\",\"iss\":\"{options.Issuer}\",\"aud\":\"{options.Audience}\",\"exp\":{DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds()}}}");

        var result = await new JsonWebTokenHandler().ValidateTokenAsync($"{header}.{payload}.", Parameters(options));

        Assert.False(result.IsValid);
    }
}
