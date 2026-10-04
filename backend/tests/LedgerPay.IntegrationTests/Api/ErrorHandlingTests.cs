using System.Net;
using LedgerPay.Domain.Constants;
using LedgerPay.IntegrationTests.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LedgerPay.IntegrationTests.Api;

public class ErrorHandlingTests(SqlServerFixture sql)
{
    [Fact]
    public async Task An_unexpected_exception_answers_500_in_the_problem_shape_without_any_detail()
    {
        var response = await sql.Api.CreateClient().GetAsync("/api/v1/probe/boom", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain("secret detail", text);
        Assert.DoesNotContain("InvalidOperationException", text);
        Assert.DoesNotContain("   at ", text);
        Assert.DoesNotContain("LedgerPay.", text);
        using var body = await ApiCalls.ReadAsync(response);
        Assert.Equal(ErrorCodes.InternalError, body.RootElement.GetProperty("code").GetString());
        Assert.Equal(500, body.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(response.Headers.GetValues("X-Correlation-Id").Single(), body.RootElement.GetProperty("traceId").GetString());
    }

    [Fact]
    public async Task A_database_error_is_logged_without_the_text_the_database_put_in_it()
    {
        var client = sql.Api.CreateClient();

        var response = await client.GetAsync("/api/v1/probe/db-boom", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("leaky.person@example.com", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.DoesNotContain(sql.Api.Logs.Lines, line => line.Contains("leaky.person@example.com"));
        Assert.Contains(sql.Api.Logs.Lines, line => line.Contains(response.Headers.GetValues("X-Correlation-Id").Single()));
    }

    [Fact]
    public async Task Parallel_registrations_for_one_email_leave_no_email_or_phone_in_the_logs()
    {
        var client = sql.Api.CreateClient();

        // One round does not always make two requests meet at the unique index, so several rounds are tried.
        for (var round = 0; round < 5; round++)
        {
            var email = ApiCalls.NewEmail();
            var phone = ApiCalls.NewPhone();

            var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => ApiCalls.RegisterAsync(client, ApiCalls.RegisterBody(email, phone))));

            Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.Created));
            Assert.DoesNotContain(sql.Api.Logs.Lines, line => line.Contains(email));
            Assert.DoesNotContain(sql.Api.Logs.Lines, line => line.Contains(phone));
        }
    }

    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore.Update")]
    [InlineData("Microsoft.EntityFrameworkCore.Database.Command")]
    public void The_logs_of_database_updates_and_commands_are_switched_off(string category)
    {
        // Their text holds the values of the failed statement, such as the email that broke a unique index.
        var configuration = sql.Api.Services.GetRequiredService<IConfiguration>();

        Assert.Equal("None", configuration[$"Logging:LogLevel:{category}"]);
    }

    [Fact]
    public async Task A_client_error_raised_inside_the_pipeline_keeps_its_status_and_the_standard_shape()
    {
        var response = await sql.Api.CreateClient().GetAsync("/api/v1/probe/bad-request", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = await ApiCalls.ReadAsync(response);
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("code").GetString()));
        Assert.DoesNotContain("Request body too large", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("GET", "/api/v1/does-not-exist", null, 404, "NOT_FOUND")]
    [InlineData("GET", "/api/v1/auth/login", null, 405, "METHOD_NOT_ALLOWED")]
    [InlineData("POST", "/api/v1/auth/login", "text/plain", 415, "UNSUPPORTED_MEDIA_TYPE")]
    public async Task Errors_from_the_framework_use_the_standard_shape_with_a_stable_code(
        string method, string path, string? contentType, int status, string code)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (contentType is not null)
        {
            request.Content = new StringContent("hello", System.Text.Encoding.UTF8, contentType);
        }

        var response = await sql.Api.CreateClient().SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = await ApiCalls.ReadAsync(response);
        Assert.Equal(code, body.RootElement.GetProperty("code").GetString());
        Assert.Equal(status, body.RootElement.GetProperty("status").GetInt32());
        Assert.False(body.RootElement.TryGetProperty("type", out _));
        Assert.Equal(response.Headers.GetValues("X-Correlation-Id").Single(), body.RootElement.GetProperty("traceId").GetString());
    }

    [Fact]
    public async Task The_trace_id_matches_the_header_for_every_kind_of_error_answer()
    {
        var client = sql.Api.CreateClient();
        var customerToken = await ApiCalls.NewCustomerTokenAsync(client);
        var email = ApiCalls.NewEmail();
        await ApiCalls.RegisterAsync(client, ApiCalls.RegisterBody(email));
        for (var i = 0; i < 5; i++)
        {
            await ApiCalls.LoginAsync(client, email, "Wrong-Password-1!");
        }

        var answers = new[]
        {
            await ApiCalls.RegisterAsync(client, new { }),
            await ApiCalls.LoginAsync(client, email, ApiCalls.Password),
            await ApiCalls.GetAsync(client, "/api/v1/probe/signed-in", token: null),
            await ApiCalls.GetAsync(client, "/api/v1/probe/operators", customerToken)
        };

        Assert.Equal([400, 423, 401, 403], answers.Select(answer => (int)answer.StatusCode));
        foreach (var answer in answers)
        {
            using var body = await ApiCalls.ReadAsync(answer);
            Assert.Equal(answer.Headers.GetValues("X-Correlation-Id").Single(), body.RootElement.GetProperty("traceId").GetString());
        }
    }
}
