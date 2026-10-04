using System.Net;
using System.Net.Http.Json;
using LedgerPay.Domain.Constants;
using LedgerPay.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace LedgerPay.IntegrationTests.Api;

public class AuthEndpointsTests(SqlServerFixture sql)
{
    private HttpClient Client() => sql.Api.CreateClient();

    // ---- register

    [Fact]
    public async Task Registering_answers_201_with_the_new_wallet_number()
    {
        var email = ApiCalls.NewEmail();
        var phone = ApiCalls.NewPhone();

        var response = await ApiCalls.RegisterAsync(Client(), ApiCalls.RegisterBody(email, phone));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var body = await ApiCalls.ReadAsync(response);
        Assert.Equal(email, body.RootElement.GetProperty("email").GetString());
        Assert.Equal(phone, body.RootElement.GetProperty("phone").GetString());
        Assert.Matches("^[1-9][0-9]{11}$", body.RootElement.GetProperty("walletNumber").GetString());
        Assert.False(body.RootElement.TryGetProperty("password", out _));
        Assert.False(body.RootElement.TryGetProperty("passwordHash", out _));
        Assert.False(body.RootElement.TryGetProperty("id", out _));
    }

    [Fact]
    public async Task The_correlation_id_the_client_sends_is_echoed_and_written_to_the_audit_log()
    {
        var email = ApiCalls.NewEmail();
        var correlationId = "test-corr-" + Guid.NewGuid().ToString("N");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/register")
        {
            Content = JsonContent.Create(ApiCalls.RegisterBody(email))
        };
        request.Headers.Add("X-Correlation-Id", correlationId);

        var response = await Client().SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(correlationId, response.Headers.GetValues("X-Correlation-Id").Single());
        await using var check = sql.NewContext();
        var audit = await check.AuditLogs.AsNoTracking().SingleAsync(
            log => log.CorrelationId == correlationId && log.Action == AuditActions.Register, TestContext.Current.CancellationToken);
        Assert.Equal(FakeRemoteIpStartupFilter.Address, audit.IpAddress);
    }

    [Fact]
    public async Task A_correlation_id_that_is_not_safe_is_replaced()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/probe/anyone");
        request.Headers.Add("X-Correlation-Id", "bad id with spaces <script>");

        var response = await Client().SendAsync(request, TestContext.Current.CancellationToken);

        var echoed = response.Headers.GetValues("X-Correlation-Id").Single();
        Assert.Matches("^[A-Za-z0-9_-]{1,64}$", echoed);
        Assert.NotEqual("bad id with spaces <script>", echoed);
    }

    [Fact]
    public async Task Registering_with_a_taken_email_answers_409_in_the_problem_shape()
    {
        var client = Client();
        var email = ApiCalls.NewEmail();
        await ApiCalls.RegisterAsync(client, ApiCalls.RegisterBody(email));

        var response = await ApiCalls.RegisterAsync(client, ApiCalls.RegisterBody(email.ToUpperInvariant()));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = await ApiCalls.ReadAsync(response);
        Assert.Equal(ErrorCodes.EmailAlreadyRegistered, body.RootElement.GetProperty("code").GetString());
        Assert.Equal(409, body.RootElement.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("title").GetString()));
        Assert.Equal(response.Headers.GetValues("X-Correlation-Id").Single(), body.RootElement.GetProperty("traceId").GetString());
    }

    [Fact]
    public async Task Registering_with_a_taken_phone_answers_409()
    {
        var client = Client();
        var phone = ApiCalls.NewPhone();
        await ApiCalls.RegisterAsync(client, ApiCalls.RegisterBody(phone: phone));

        var response = await ApiCalls.RegisterAsync(client, ApiCalls.RegisterBody(phone: phone));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var body = await ApiCalls.ReadAsync(response);
        Assert.Equal(ErrorCodes.PhoneAlreadyRegistered, body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Registering_with_bad_fields_answers_400_with_a_map_of_the_fields()
    {
        var response = await ApiCalls.RegisterAsync(
            Client(), new { fullName = "N", email = "not-an-email", phone = "0771234567", password = "short" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = await ApiCalls.ReadAsync(response);
        Assert.Equal(ErrorCodes.ValidationFailed, body.RootElement.GetProperty("code").GetString());
        var errors = body.RootElement.GetProperty("errors");
        foreach (var field in new[] { "fullName", "email", "phone", "password" })
        {
            Assert.True(errors.TryGetProperty(field, out var messages), $"errors should have {field}");
            Assert.True(messages.GetArrayLength() > 0);
        }
    }

    [Fact]
    public async Task Registering_with_fields_missing_answers_400_with_the_message_of_each_field()
    {
        var response = await Client().PostAsync(
            "/api/v1/auth/register", new StringContent("{}", System.Text.Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = await ApiCalls.ReadAsync(response);
        Assert.Equal(ErrorCodes.ValidationFailed, body.RootElement.GetProperty("code").GetString());
        var errors = body.RootElement.GetProperty("errors");
        Assert.Equal(["email", "fullName", "password", "phone"], errors.EnumerateObject().Select(field => field.Name).Order());
        Assert.Contains("password", errors.GetProperty("password")[0].GetString()!, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual("The value is not valid.", errors.GetProperty("fullName")[0].GetString());
    }

    [Theory]
    [InlineData("{\"fullName\": 5, \"email\": \"a@b.com\", \"phone\": \"+94771234567\", \"password\": \"Kandy-Lake-2026!\"}", "fullName")]
    [InlineData("[1, 2, 3]", "body")]
    [InlineData("\"just a string\"", "body")]
    public async Task A_body_of_the_wrong_type_names_only_fields_the_client_knows(string content, string expectedField)
    {
        var response = await Client().PostAsync(
            "/api/v1/auth/register", new StringContent(content, System.Text.Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = await ApiCalls.ReadAsync(response);
        var names = body.RootElement.GetProperty("errors").EnumerateObject().Select(field => field.Name).ToList();
        Assert.Contains(expectedField, names);
        Assert.All(names, name => Assert.DoesNotContain(name, new[] { "request", "$", "" }));
    }

    [Theory]
    [InlineData("this is not json")]
    [InlineData("{\"fullName\": ")]
    [InlineData("")]
    public async Task A_body_that_is_not_json_answers_400_without_parser_details(string content)
    {
        var response = await Client().PostAsync(
            "/api/v1/auth/register", new StringContent(content, System.Text.Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains(ErrorCodes.ValidationFailed, text);
        Assert.DoesNotContain("System.Text.Json", text);
        Assert.DoesNotContain("LineNumber", text);
        Assert.DoesNotContain("BytePositionInLine", text);
    }

    [Fact]
    public async Task The_register_route_is_only_under_api_v1()
    {
        var response = await Client().PostAsJsonAsync("/auth/register", ApiCalls.RegisterBody(), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- login

    [Fact]
    public async Task Signing_in_answers_200_with_a_token_the_roles_and_the_wallet()
    {
        var client = Client();
        var email = ApiCalls.NewEmail();
        await ApiCalls.RegisterAsync(client, ApiCalls.RegisterBody(email));

        var response = await ApiCalls.LoginAsync(client, email, ApiCalls.Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await ApiCalls.ReadAsync(response);
        Assert.Equal("Bearer", body.RootElement.GetProperty("tokenType").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("accessToken").GetString()));
        Assert.Equal("Customer", body.RootElement.GetProperty("roles")[0].GetString());
        Assert.Matches("^[0-9]{12}$", body.RootElement.GetProperty("walletNumber").GetString());
        var expiresAt = body.RootElement.GetProperty("expiresAt").GetDateTime();
        Assert.InRange((expiresAt - DateTime.UtcNow).TotalMinutes, 14.0, 15.1);
    }

    [Fact]
    public async Task A_wrong_password_answers_401_in_the_problem_shape()
    {
        var client = Client();
        var email = ApiCalls.NewEmail();
        await ApiCalls.RegisterAsync(client, ApiCalls.RegisterBody(email));

        var response = await ApiCalls.LoginAsync(client, email, "Wrong-Password-1!");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = await ApiCalls.ReadAsync(response);
        Assert.Equal(ErrorCodes.InvalidCredentials, body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task An_unknown_email_and_a_wrong_password_look_the_same_from_outside()
    {
        var client = Client();
        var email = ApiCalls.NewEmail();
        await ApiCalls.RegisterAsync(client, ApiCalls.RegisterBody(email));

        var wrong = await ApiCalls.LoginAsync(client, email, "Wrong-Password-1!");
        var unknown = await ApiCalls.LoginAsync(client, ApiCalls.NewEmail(), "Wrong-Password-1!");

        Assert.Equal(wrong.StatusCode, unknown.StatusCode);
        Assert.Equal(wrong.Content.Headers.ContentType, unknown.Content.Headers.ContentType);
        Assert.Equal(
            wrong.Headers.Select(header => header.Key).Where(name => name != "X-Correlation-Id").Order(),
            unknown.Headers.Select(header => header.Key).Where(name => name != "X-Correlation-Id").Order());
        var wrongBody = await ApiCalls.WithoutTraceIdAsync(wrong);
        var unknownBody = await ApiCalls.WithoutTraceIdAsync(unknown);
        Assert.Equal(wrongBody, unknownBody);
    }

    [Fact]
    public async Task Five_wrong_passwords_lock_the_account_and_the_answer_says_how_long()
    {
        var client = Client();
        var email = ApiCalls.NewEmail();
        await ApiCalls.RegisterAsync(client, ApiCalls.RegisterBody(email));
        HttpResponseMessage last = null!;
        for (var i = 0; i < 5; i++)
        {
            last = await ApiCalls.LoginAsync(client, email, "Wrong-Password-1!");
        }

        var rightPassword = await ApiCalls.LoginAsync(client, email, ApiCalls.Password);

        Assert.Equal((HttpStatusCode)423, last.StatusCode);
        Assert.Equal("900", last.Headers.GetValues("Retry-After").Single());
        Assert.Equal((HttpStatusCode)423, rightPassword.StatusCode);
        using var body = await ApiCalls.ReadAsync(rightPassword);
        Assert.Equal(ErrorCodes.AccountLocked, body.RootElement.GetProperty("code").GetString());
        Assert.InRange(body.RootElement.GetProperty("retryAfterSeconds").GetInt32(), 1, 900);
    }

    [Fact]
    public async Task Signing_in_without_a_password_answers_400()
    {
        var response = await Client().PostAsJsonAsync(
            "/api/v1/auth/login", new { email = "a@b.com", password = "" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = await ApiCalls.ReadAsync(response);
        Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("password", out _));
    }

    [Fact]
    public async Task Signing_in_writes_an_audit_entry_with_the_request_ip()
    {
        var client = Client();
        var email = ApiCalls.NewEmail();
        await ApiCalls.RegisterAsync(client, ApiCalls.RegisterBody(email));

        await ApiCalls.LoginAsync(client, email, ApiCalls.Password);

        await using var check = sql.NewContext();
        var user = await check.Users.AsNoTracking().SingleAsync(u => u.Email == email, TestContext.Current.CancellationToken);
        var audit = await check.AuditLogs.AsNoTracking().SingleAsync(
            log => log.ActorUserId == user.Id && log.Action == AuditActions.LoginSucceeded, TestContext.Current.CancellationToken);
        Assert.Equal(FakeRemoteIpStartupFilter.Address, audit.IpAddress);
        Assert.False(string.IsNullOrWhiteSpace(audit.CorrelationId));
    }
}
