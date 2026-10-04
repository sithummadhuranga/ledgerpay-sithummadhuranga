using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LedgerPay.Infrastructure.Seeding;

namespace LedgerPay.IntegrationTests.Support;

internal static class ApiCalls
{
    private static long nextPhone = 60_000_000;

    public const string Password = "Kandy-Lake-2026!";

    public static string NewEmail() => $"http.{Guid.NewGuid():N}@example.com";

    public static string NewPhone() => "+947" + Interlocked.Increment(ref nextPhone);

    public static object RegisterBody(string? email = null, string? phone = null) => new
    {
        fullName = "Nimali Perera",
        email = email ?? NewEmail(),
        phone = phone ?? NewPhone(),
        password = Password
    };

    public static Task<HttpResponseMessage> RegisterAsync(HttpClient client, object body) =>
        client.PostAsJsonAsync("/api/v1/auth/register", body, TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password) =>
        client.PostAsJsonAsync("/api/v1/auth/login", new { email, password }, TestContext.Current.CancellationToken);

    public static async Task<JsonDocument> ReadAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

    // The whole body as text, without the trace id, which is different on every request.
    public static async Task<string> WithoutTraceIdAsync(HttpResponseMessage response)
    {
        using var body = await ReadAsync(response);
        var fields = body.RootElement.EnumerateObject().Where(field => field.Name != "traceId").Select(field => $"{field.Name}={field.Value}");
        return string.Join("|", fields.Order());
    }

    public static async Task<string> NewCustomerTokenAsync(HttpClient client)
    {
        var email = NewEmail();
        (await RegisterAsync(client, RegisterBody(email))).EnsureSuccessStatusCode();
        return await TokenAsync(client, email, Password);
    }

    public static async Task<string> TokenAsync(HttpClient client, string email, string password)
    {
        var response = await LoginAsync(client, email, password);
        response.EnsureSuccessStatusCode();
        using var body = await ReadAsync(response);
        return body.RootElement.GetProperty("accessToken").GetString()!;
    }

    public static HttpRequestMessage WithBearer(HttpMethod method, string url, string? token)
    {
        var request = new HttpRequestMessage(method, url);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return request;
    }

    public static Task<string> OperatorTokenAsync(HttpClient client) =>
        TokenAsync(client, SeedData.OperatorEmail, SqlServerFixture.TestSeedOptions.OperatorPassword);

    public static Task<string> AdminTokenAsync(HttpClient client) =>
        TokenAsync(client, SeedData.AdminEmail, SqlServerFixture.TestSeedOptions.AdminPassword);

    public static string NewKey() => Guid.NewGuid().ToString();

    public static string NewBankReference() => "BANK" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();

    public static Task<HttpResponseMessage> SendAsync(
        HttpClient client, HttpMethod method, string url, string? token, object? body = null, string? idempotencyKey = null)
    {
        var request = WithBearer(method, url, token);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        return client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    public static Task<HttpResponseMessage> TopUpAsync(
        HttpClient client, string operatorToken, string walletNumber, decimal amount, string? bankReference = null, string? key = null) =>
        SendAsync(client, HttpMethod.Post, "/api/v1/admin/topups", operatorToken,
            new { walletNumber, amount, bankReference = bankReference ?? NewBankReference() }, key ?? NewKey());

    public static Task<HttpResponseMessage> TransferAsync(
        HttpClient client, string token, object body, string? key = null) =>
        SendAsync(client, HttpMethod.Post, "/api/v1/transfers", token, body, key ?? NewKey());

    public static Task<HttpResponseMessage> SetStatusAsync(
        HttpClient client, string token, string walletNumber, object body) =>
        SendAsync(client, HttpMethod.Patch, $"/api/v1/admin/wallets/{walletNumber}/status", token, body);

    // Registers a customer, signs in, and tops the wallet up through the operator endpoint when asked to.
    public static async Task<TestCustomer> NewCustomerAsync(HttpClient client, decimal fundedWith = 0m)
    {
        var email = NewEmail();
        var phone = NewPhone();
        var registered = await RegisterAsync(client, RegisterBody(email, phone));
        registered.EnsureSuccessStatusCode();
        using var registeredBody = await ReadAsync(registered);
        var walletNumber = registeredBody.RootElement.GetProperty("walletNumber").GetString()!;

        var token = await TokenAsync(client, email, Password);
        if (fundedWith > 0)
        {
            var operatorToken = await OperatorTokenAsync(client);
            (await TopUpAsync(client, operatorToken, walletNumber, fundedWith)).EnsureSuccessStatusCode();
        }

        return new TestCustomer(email, phone, walletNumber, token);
    }

    public static Task<HttpResponseMessage> GetAsync(HttpClient client, string url, string? token) =>
        client.SendAsync(WithBearer(HttpMethod.Get, url, token), TestContext.Current.CancellationToken);
}

internal sealed record TestCustomer(string Email, string Phone, string WalletNumber, string Token);
