using System.Net;
using LedgerPay.IntegrationTests.Support;
using Microsoft.AspNetCore.Hosting;

namespace LedgerPay.IntegrationTests.Api;

public class CorsAndHeadersTests(SqlServerFixture sql)
{
    private static HttpRequestMessage Preflight(string origin, string path = "/api/v1/auth/login")
    {
        var request = new HttpRequestMessage(HttpMethod.Options, path);
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type,idempotency-key");
        return request;
    }

    [Fact]
    public async Task The_frontend_origin_may_call_the_api_with_the_headers_it_needs()
    {
        var response = await sql.Api.CreateClient().SendAsync(Preflight(ApiFactory.AllowedOrigin), TestContext.Current.CancellationToken);

        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal(ApiFactory.AllowedOrigin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        var allowedHeaders = string.Join(",", response.Headers.GetValues("Access-Control-Allow-Headers")).ToLowerInvariant();
        Assert.Contains("authorization", allowedHeaders);
        Assert.Contains("idempotency-key", allowedHeaders);
        Assert.Contains("content-type", allowedHeaders);
        Assert.Contains("POST", string.Join(",", response.Headers.GetValues("Access-Control-Allow-Methods")));
    }

    [Fact]
    public async Task The_frontend_may_send_a_patch_to_the_status_route()
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/admin/wallets/100000000000/status");
        request.Headers.Add("Origin", ApiFactory.AllowedOrigin);
        request.Headers.Add("Access-Control-Request-Method", "PATCH");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");

        var response = await sql.Api.CreateClient().SendAsync(request, TestContext.Current.CancellationToken);

        Assert.True(response.IsSuccessStatusCode);
        Assert.Contains("PATCH", string.Join(",", response.Headers.GetValues("Access-Control-Allow-Methods")));
    }

    [Theory]
    [InlineData("https://evil.example")]
    [InlineData("http://localhost:5174")]
    [InlineData("null")]
    public async Task Any_other_origin_gets_no_permission(string origin)
    {
        var response = await sql.Api.CreateClient().SendAsync(Preflight(origin), TestContext.Current.CancellationToken);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task A_plain_request_from_another_origin_gets_no_permission_header()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/probe/anyone");
        request.Headers.Add("Origin", "https://evil.example");

        var response = await sql.Api.CreateClient().SendAsync(request, TestContext.Current.CancellationToken);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task The_headers_the_browser_needs_to_read_are_exposed_to_the_frontend()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/probe/anyone");
        request.Headers.Add("Origin", ApiFactory.AllowedOrigin);

        var response = await sql.Api.CreateClient().SendAsync(request, TestContext.Current.CancellationToken);

        var exposed = string.Join(",", response.Headers.GetValues("Access-Control-Expose-Headers")).ToLowerInvariant();
        Assert.Contains("retry-after", exposed);
        Assert.Contains("idempotent-replayed", exposed);
        Assert.Contains("x-correlation-id", exposed);
    }

    [Fact]
    public async Task Every_response_carries_the_security_headers()
    {
        var response = await sql.Api.CreateClient().GetAsync("/api/v1/probe/anyone", TestContext.Current.CancellationToken);

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task Error_answers_carry_the_security_headers_too()
    {
        var response = await sql.Api.CreateClient().GetAsync("/api/v1/probe/boom", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
    }

    [Theory]
    [InlineData("*")]
    [InlineData("")]
    [InlineData("http://localhost:5173/")]
    [InlineData("http://localhost:5173/app")]
    [InlineData("ftp://localhost")]
    [InlineData("localhost:5173")]
    [InlineData("https://*.example.com")]
    public void The_app_does_not_start_with_an_allowed_origin_that_is_not_a_plain_origin(string origin)
    {
        var factory = sql.Api.WithWebHostBuilder(builder => builder.UseSetting("Cors:AllowedOrigins:0", origin));

        var error = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("Cors:AllowedOrigins", Flatten(error));
    }

    [Fact]
    public void The_app_does_not_start_without_a_signing_key()
    {
        var factory = sql.Api.WithWebHostBuilder(builder => builder.UseSetting("Jwt:SigningKey", ""));

        var error = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("Jwt:SigningKey", Flatten(error));
    }

    [Fact]
    public void The_app_does_not_start_without_a_database_connection()
    {
        var factory = sql.Api.WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:Api", ""));

        var error = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("ConnectionStrings:Api", Flatten(error));
    }

    private static string Flatten(Exception error)
    {
        var messages = new List<string>();
        for (var current = error; current is not null; current = current.InnerException!)
        {
            messages.Add(current.Message);
            if (current.InnerException is null)
            {
                break;
            }
        }

        return string.Join(" | ", messages);
    }
}
