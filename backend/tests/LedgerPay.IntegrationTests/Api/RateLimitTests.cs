using System.Net;
using System.Net.Http.Json;
using LedgerPay.Domain.Constants;
using LedgerPay.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace LedgerPay.IntegrationTests.Api;

public class RateLimitTests(SqlServerFixture sql)
{
    private ApiFactory Limited(string area, int permits, int windowSeconds = 60, bool forwarded = false) =>
        new(sql.ApiConnectionString, TestJwt.Options(), new Dictionary<string, string?>
        {
            [$"RateLimits:{area}:PermitLimit"] = permits.ToString(),
            [$"RateLimits:{area}:WindowSeconds"] = windowSeconds.ToString(),
            ["ForwardedHeaders:Enabled"] = forwarded ? "true" : "false"
        });

    private static Task<HttpResponseMessage> LoginFromAsync(HttpClient client, string email, string? forwardedFor = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login") { Content = JsonContent.Create(new { email, password = "Wrong-Password-1!" }) };
        if (forwardedFor is not null)
        {
            request.Headers.Add("X-Forwarded-For", forwardedFor);
        }

        return client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Sign_in_past_the_limit_answers_429_in_the_standard_shape_with_a_wait_time()
    {
        await using var factory = Limited("Auth", 3);
        var client = factory.CreateClient();
        var email = ApiCalls.NewEmail();

        var within = new List<HttpStatusCode>();
        for (var i = 0; i < 3; i++)
        {
            within.Add((await LoginFromAsync(client, email)).StatusCode);
        }

        var over = await LoginFromAsync(client, email);

        Assert.All(within, status => Assert.Equal(HttpStatusCode.Unauthorized, status));
        Assert.Equal(HttpStatusCode.TooManyRequests, over.StatusCode);
        Assert.Equal("application/problem+json", over.Content.Headers.ContentType?.MediaType);
        var wait = int.Parse(over.Headers.GetValues("Retry-After").Single());
        Assert.InRange(wait, 1, 60);
        using var body = await ApiCalls.ReadAsync(over);
        Assert.Equal(ErrorCodes.RateLimited, body.RootElement.GetProperty("code").GetString());
        Assert.Equal(429, body.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(wait, body.RootElement.GetProperty("retryAfterSeconds").GetInt32());
        Assert.Equal(over.Headers.GetValues("X-Correlation-Id").Single(), body.RootElement.GetProperty("traceId").GetString());
        Assert.Equal("nosniff", over.Headers.GetValues("X-Content-Type-Options").Single());
    }

    [Fact]
    public async Task Register_and_sign_in_share_one_allowance_per_address()
    {
        await using var factory = Limited("Auth", 2);
        var client = factory.CreateClient();

        var first = await ApiCalls.RegisterAsync(client, ApiCalls.RegisterBody());
        var second = await ApiCalls.LoginAsync(client, ApiCalls.NewEmail(), "Wrong-Password-1!");
        var third = await ApiCalls.RegisterAsync(client, ApiCalls.RegisterBody());

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
    }

    [Fact]
    public async Task A_request_refused_for_rate_never_reaches_the_service()
    {
        await using var factory = Limited("Auth", 1);
        var client = factory.CreateClient();
        var email = ApiCalls.NewEmail();
        await LoginFromAsync(client, email);

        var refused = await ApiCalls.RegisterAsync(client, ApiCalls.RegisterBody(email));

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        await using var db = sql.NewContext();
        Assert.False(await db.Users.AnyAsync(user => user.Email == email, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task The_wait_ends_and_the_next_request_is_let_through()
    {
        await using var factory = Limited("Auth", 1, windowSeconds: 1);
        var client = factory.CreateClient();
        var email = ApiCalls.NewEmail();
        await LoginFromAsync(client, email);
        var refused = await LoginFromAsync(client, email);

        await Task.Delay(TimeSpan.FromSeconds(int.Parse(refused.Headers.GetValues("Retry-After").Single())) + TimeSpan.FromMilliseconds(300), TestContext.Current.CancellationToken);
        var again = await LoginFromAsync(client, email);

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, again.StatusCode);
    }

    [Fact]
    public async Task Lookups_are_counted_for_each_signed_in_customer_on_their_own()
    {
        await using var factory = Limited("Lookup", 2);
        var client = factory.CreateClient();
        var first = await ApiCalls.NewCustomerAsync(client);
        var second = await ApiCalls.NewCustomerAsync(client);
        var url = $"/api/v1/wallets/lookup?walletNumber={second.WalletNumber}";

        var firstOne = await ApiCalls.GetAsync(client, url, first.Token);
        var firstTwo = await ApiCalls.GetAsync(client, url, first.Token);
        var firstThree = await ApiCalls.GetAsync(client, url, first.Token);
        var otherCustomer = await ApiCalls.GetAsync(client, url, second.Token);

        Assert.Equal(HttpStatusCode.OK, firstOne.StatusCode);
        Assert.Equal(HttpStatusCode.OK, firstTwo.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, firstThree.StatusCode);
        Assert.Equal(HttpStatusCode.OK, otherCustomer.StatusCode);
    }

    [Fact]
    public async Task Money_routes_share_one_allowance_per_user_and_a_refused_transfer_moves_nothing()
    {
        await using var factory = Limited("Money", 2);
        var client = factory.CreateClient();
        var sender = await ApiCalls.NewCustomerAsync(client, 5000m);
        var receiver = await ApiCalls.NewCustomerAsync(client);

        var quote = await ApiCalls.GetAsync(client, "/api/v1/transfers/quote?amount=1000", sender.Token);
        var transfer = await ApiCalls.TransferAsync(client, sender.Token, new { recipientWalletNumber = receiver.WalletNumber, amount = 1000m });
        var refused = await ApiCalls.TransferAsync(client, sender.Token, new { recipientWalletNumber = receiver.WalletNumber, amount = 1000m });

        Assert.Equal(HttpStatusCode.OK, quote.StatusCode);
        Assert.Equal(HttpStatusCode.Created, transfer.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        await using var db = sql.NewContext();
        var balance = await db.Wallets.AsNoTracking().Where(wallet => wallet.WalletNumber == sender.WalletNumber).Select(wallet => wallet.Balance)
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(5000m - 1010m, balance);
    }

    [Fact]
    public async Task Top_ups_are_counted_for_the_operator()
    {
        await using var factory = Limited("Money", 1);
        var client = factory.CreateClient();
        var customer = await ApiCalls.NewCustomerAsync(client);
        var operatorToken = await ApiCalls.OperatorTokenAsync(client);

        var first = await ApiCalls.TopUpAsync(client, operatorToken, customer.WalletNumber, 100m);
        var second = await ApiCalls.TopUpAsync(client, operatorToken, customer.WalletNumber, 100m);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
    }

    [Fact]
    public async Task A_request_with_no_valid_token_is_refused_for_that_and_does_not_use_up_the_limit()
    {
        await using var factory = Limited("Money", 1);
        var client = factory.CreateClient();
        var customer = await ApiCalls.NewCustomerAsync(client);

        var anonymous = new List<HttpStatusCode>();
        for (var i = 0; i < 4; i++)
        {
            anonymous.Add((await ApiCalls.GetAsync(client, "/api/v1/transfers/quote?amount=1000", null)).StatusCode);
        }

        var signedIn = await ApiCalls.GetAsync(client, "/api/v1/transfers/quote?amount=1000", customer.Token);

        Assert.All(anonymous, status => Assert.Equal(HttpStatusCode.Unauthorized, status));
        Assert.Equal(HttpStatusCode.OK, signedIn.StatusCode);
    }

    [Fact]
    public async Task The_health_check_and_the_api_description_are_never_limited()
    {
        await using var factory = new ApiFactory(sql.ApiConnectionString, TestJwt.Options(), new Dictionary<string, string?>
        {
            ["RateLimits:Auth:PermitLimit"] = "1",
            ["RateLimits:Lookup:PermitLimit"] = "1",
            ["RateLimits:Money:PermitLimit"] = "1"
        });
        var client = factory.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 5; i++)
        {
            statuses.Add((await client.GetAsync("/api/v1/health", TestContext.Current.CancellationToken)).StatusCode);
            statuses.Add((await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken)).StatusCode);
        }

        Assert.All(statuses, status => Assert.Equal(HttpStatusCode.OK, status));
    }

    [Fact]
    public async Task Each_forwarded_address_has_its_own_allowance_when_forwarding_is_on()
    {
        await using var factory = Limited("Auth", 1, forwarded: true);
        var client = factory.CreateClient();
        var email = ApiCalls.NewEmail();

        var firstAddress = await LoginFromAsync(client, email, "198.51.100.1");
        var firstAgain = await LoginFromAsync(client, email, "198.51.100.1");
        var secondAddress = await LoginFromAsync(client, email, "198.51.100.2");

        Assert.Equal(HttpStatusCode.Unauthorized, firstAddress.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, firstAgain.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, secondAddress.StatusCode);
    }

    [Fact]
    public async Task A_forwarded_address_cannot_be_used_to_dodge_the_limit_when_forwarding_is_off()
    {
        await using var factory = Limited("Auth", 2, forwarded: false);
        var client = factory.CreateClient();
        var email = ApiCalls.NewEmail();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 3; i++)
        {
            statuses.Add((await LoginFromAsync(client, email, $"198.51.100.{i + 10}")).StatusCode);
        }

        Assert.Equal([HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests], statuses);
    }
}
