using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

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

    public static Task<HttpResponseMessage> GetAsync(HttpClient client, string url, string? token) =>
        client.SendAsync(WithBearer(HttpMethod.Get, url, token), TestContext.Current.CancellationToken);
}
