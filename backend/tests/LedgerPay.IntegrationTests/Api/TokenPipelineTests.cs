using System.Net;
using System.Text;
using LedgerPay.Domain.Constants;
using LedgerPay.Infrastructure.Security;
using LedgerPay.IntegrationTests.Support;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace LedgerPay.IntegrationTests.Api;

public class TokenPipelineTests(SqlServerFixture sql)
{
    private const string SignedIn = "/api/v1/probe/signed-in";
    private const string OperatorsOnly = "/api/v1/probe/operators";

    private HttpClient Client() => sql.Api.CreateClient();

    private JwtOptions Real => sql.Api.Jwt;

    private async Task AssertUnauthenticatedAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = await ApiCalls.ReadAsync(response);
        Assert.Equal(ErrorCodes.Unauthenticated, body.RootElement.GetProperty("code").GetString());
    }

    private string TokenWith(Action<SecurityTokenDescriptor> change, string? signingKey = null, string algorithm = SecurityAlgorithms.HmacSha256)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = Real.Issuer,
            Audience = Real.Audience,
            Expires = DateTime.UtcNow.AddMinutes(10),
            Claims = new Dictionary<string, object>
            {
                ["sub"] = Guid.NewGuid().ToString(),
                ["role"] = new[] { "Customer" }
            },
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(signingKey is null ? Real.SigningKeyBytes() : Convert.FromBase64String(signingKey)), algorithm)
        };
        change(descriptor);
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    [Fact]
    public async Task An_endpoint_with_no_attribute_still_asks_for_a_token()
    {
        var client = sql.Api.CreateClient();

        var anonymous = await ApiCalls.GetAsync(client, "/api/v1/probe/unmarked", token: null);
        var signedIn = await ApiCalls.GetAsync(client, "/api/v1/probe/unmarked", await ApiCalls.NewCustomerTokenAsync(client));

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.OK, signedIn.StatusCode);
    }

    [Fact]
    public async Task No_token_answers_401_in_the_problem_shape_with_a_bearer_challenge()
    {
        var response = await ApiCalls.GetAsync(Client(), SignedIn, token: null);

        await AssertUnauthenticatedAsync(response);
        Assert.Contains("Bearer", response.Headers.WwwAuthenticate.Select(header => header.Scheme));
    }

    [Fact]
    public async Task A_token_from_signing_in_opens_a_protected_endpoint_and_carries_the_roles()
    {
        var client = Client();
        var token = await ApiCalls.NewCustomerTokenAsync(client);

        var response = await ApiCalls.GetAsync(client, SignedIn, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await ApiCalls.ReadAsync(response);
        Assert.True(Guid.TryParse(body.RootElement.GetProperty("userId").GetString(), out _));
        Assert.Equal("Customer", body.RootElement.GetProperty("roles")[0].GetString());
    }

    [Fact]
    public async Task An_anonymous_endpoint_needs_no_token()
    {
        var response = await ApiCalls.GetAsync(Client(), "/api/v1/probe/anyone", token: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_customer_is_refused_an_operator_endpoint_with_403_in_the_problem_shape()
    {
        var client = Client();
        var token = await ApiCalls.NewCustomerTokenAsync(client);

        var response = await ApiCalls.GetAsync(client, OperatorsOnly, token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = await ApiCalls.ReadAsync(response);
        Assert.Equal(ErrorCodes.Forbidden, body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task The_seeded_operator_gets_into_the_operator_endpoint()
    {
        var client = Client();
        var token = await ApiCalls.TokenAsync(client, LedgerPay.Infrastructure.Seeding.SeedData.OperatorEmail, "Operator-pass-for-tests-2!");

        var response = await ApiCalls.GetAsync(client, OperatorsOnly, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_token_that_has_expired_is_refused()
    {
        var token = TokenWith(d =>
        {
            d.NotBefore = DateTime.UtcNow.AddMinutes(-40);
            d.Expires = DateTime.UtcNow.AddMinutes(-20);
        });

        await AssertUnauthenticatedAsync(await ApiCalls.GetAsync(Client(), SignedIn, token));
    }

    [Fact]
    public async Task A_token_that_expired_two_minutes_ago_is_refused_so_the_slack_is_small()
    {
        var token = TokenWith(d =>
        {
            d.NotBefore = DateTime.UtcNow.AddMinutes(-17);
            d.Expires = DateTime.UtcNow.AddMinutes(-2);
        });

        await AssertUnauthenticatedAsync(await ApiCalls.GetAsync(Client(), SignedIn, token));
    }

    [Fact]
    public async Task A_token_signed_with_another_key_is_refused()
    {
        var token = TokenWith(_ => { }, signingKey: TestJwt.Options().SigningKey);

        await AssertUnauthenticatedAsync(await ApiCalls.GetAsync(Client(), SignedIn, token));
    }

    [Fact]
    public async Task A_token_signed_with_the_right_key_but_another_algorithm_is_refused()
    {
        var token = TokenWith(_ => { }, algorithm: SecurityAlgorithms.HmacSha512);

        await AssertUnauthenticatedAsync(await ApiCalls.GetAsync(Client(), SignedIn, token));
    }

    [Fact]
    public async Task A_token_with_no_signature_is_refused()
    {
        var header = Base64UrlEncoder.Encode("{\"alg\":\"none\",\"typ\":\"JWT\"}");
        var payload = Base64UrlEncoder.Encode(
            $"{{\"sub\":\"{Guid.NewGuid()}\",\"role\":[\"Operator\"],\"iss\":\"{Real.Issuer}\",\"aud\":\"{Real.Audience}\",\"exp\":{DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds()}}}");

        await AssertUnauthenticatedAsync(await ApiCalls.GetAsync(Client(), OperatorsOnly, $"{header}.{payload}."));
    }

    [Fact]
    public async Task A_token_for_another_audience_or_issuer_is_refused()
    {
        var wrongAudience = TokenWith(d => d.Audience = "someone-else");
        var wrongIssuer = TokenWith(d => d.Issuer = "someone-else");

        await AssertUnauthenticatedAsync(await ApiCalls.GetAsync(Client(), SignedIn, wrongAudience));
        await AssertUnauthenticatedAsync(await ApiCalls.GetAsync(Client(), SignedIn, wrongIssuer));
    }

    [Fact]
    public async Task A_token_whose_roles_were_changed_after_signing_is_refused()
    {
        var client = Client();
        var token = await ApiCalls.NewCustomerTokenAsync(client);
        var parts = token.Split('.');
        var payload = Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(parts[1])).Replace("Customer", "Operator");
        var forged = $"{parts[0]}.{Base64UrlEncoder.Encode(payload)}.{parts[2]}";

        await AssertUnauthenticatedAsync(await ApiCalls.GetAsync(client, OperatorsOnly, forged));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("a.b.c")]
    [InlineData("")]
    public async Task Garbage_in_the_authorization_header_is_refused(string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, SignedIn);
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + token);

        await AssertUnauthenticatedAsync(await Client().SendAsync(request, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_token_with_no_expiry_is_refused()
    {
        var payload = $"{{\"sub\":\"{Guid.NewGuid()}\",\"role\":[\"Customer\"],\"iss\":\"{Real.Issuer}\",\"aud\":\"{Real.Audience}\"}}";
        var token = new JsonWebTokenHandler().CreateToken(
            payload, new SigningCredentials(new SymmetricSecurityKey(Real.SigningKeyBytes()), SecurityAlgorithms.HmacSha256));

        await AssertUnauthenticatedAsync(await ApiCalls.GetAsync(Client(), SignedIn, token));
    }
}
