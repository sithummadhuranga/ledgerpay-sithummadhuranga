using System.Net;
using LedgerPay.IntegrationTests.Support;
using Microsoft.Data.SqlClient;

namespace LedgerPay.IntegrationTests.Api;

public class HealthEndpointTests(SqlServerFixture sql)
{
    private const string Url = "/api/v1/health";

    [Fact]
    public async Task Anyone_can_ask_and_a_working_database_answers_200_healthy()
    {
        var response = await sql.Api.CreateClient().GetAsync(Url, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var body = await ApiCalls.ReadAsync(response);
        Assert.Equal(["status"], body.RootElement.EnumerateObject().Select(property => property.Name));
        Assert.Equal("Healthy", body.RootElement.GetProperty("status").GetString());
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
        Assert.True(response.Headers.Contains("X-Correlation-Id"));
    }

    [Fact]
    public async Task A_database_that_cannot_be_reached_answers_503_without_saying_why()
    {
        var broken = new SqlConnectionStringBuilder(sql.ApiConnectionString) { Password = "not-the-password-1A!" }.ConnectionString;
        await using var factory = new ApiFactory(broken, TestJwt.Options());

        var response = await factory.CreateClient().GetAsync(Url, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var body = System.Text.Json.JsonDocument.Parse(text);
        Assert.Equal("Unhealthy", body.RootElement.GetProperty("status").GetString());
        Assert.DoesNotContain("Login failed", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(sql.ApiLoginName, text);
        Assert.DoesNotContain("Server=", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_check_is_only_under_the_versioned_path_and_only_for_get()
    {
        var client = sql.Api.CreateClient();

        var unversioned = await client.GetAsync("/health", TestContext.Current.CancellationToken);
        var post = await client.PostAsync(Url, content: null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, unversioned.StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, post.StatusCode);
    }
}
