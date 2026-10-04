using System.Net;
using LedgerPay.IntegrationTests.Support;
using Serilog.Events;

namespace LedgerPay.IntegrationTests.Api;

public class LoggingTests(SqlServerFixture sql)
{
    private static string? Property(LogEvent logEvent, string name) =>
        logEvent.Properties.TryGetValue(name, out var value) ? value.ToString().Trim('"') : null;

    // The line that says a request is finished, for one route pattern.
    private LogEvent? Finished(string routePattern) =>
        sql.Api.Logs.Events.LastOrDefault(logEvent => Property(logEvent, "RoutePattern") == routePattern);

    [Fact]
    public async Task A_finished_request_is_one_structured_event_with_method_route_status_time_and_trace_id()
    {
        var client = sql.Api.CreateClient();
        var customer = await ApiCalls.NewCustomerAsync(client);

        var response = await ApiCalls.GetAsync(client, "/api/v1/wallets/me", customer.Token);

        var logged = Finished("api/v1/wallets/me");
        Assert.NotNull(logged);
        Assert.Equal(LogEventLevel.Information, logged.Level);
        Assert.Equal("GET", Property(logged, "RequestMethod"));
        Assert.Equal("200", Property(logged, "StatusCode"));
        Assert.True(logged.Properties.ContainsKey("Elapsed"));
        Assert.Equal(response.Headers.GetValues("X-Correlation-Id").Single(), Property(logged, "TraceId"));
        Assert.Contains("{RoutePattern}", logged.MessageTemplate.Text);
    }

    [Fact]
    public async Task A_server_error_is_logged_as_an_error_with_the_trace_id_the_client_got()
    {
        var response = await sql.Api.CreateClient().GetAsync("/api/v1/probe/boom", TestContext.Current.CancellationToken);

        var traceId = response.Headers.GetValues("X-Correlation-Id").Single();
        var finished = Finished("api/v1/probe/boom");
        Assert.NotNull(finished);
        Assert.Equal(LogEventLevel.Error, finished.Level);
        Assert.Equal("500", Property(finished, "StatusCode"));
        Assert.Contains(sql.Api.Logs.Events, logEvent => logEvent.Exception is not null && Property(logEvent, "TraceId") == traceId);
    }

    [Fact]
    public async Task The_query_string_the_path_values_and_the_bodies_are_never_logged()
    {
        var client = sql.Api.CreateClient();
        var customer = await ApiCalls.NewCustomerAsync(client, 5000m);
        var other = await ApiCalls.NewCustomerAsync(client);
        var transfer = await ApiCalls.TransferAsync(client, customer.Token,
            new { recipientWalletNumber = other.WalletNumber, amount = 250.75m, note = "private note about rent" });
        using var created = await ApiCalls.ReadAsync(transfer);
        var reference = created.RootElement.GetProperty("reference").GetString()!;
        await ApiCalls.GetAsync(client, $"/api/v1/wallets/lookup?phone={Uri.EscapeDataString(other.Phone)}", customer.Token);
        await ApiCalls.GetAsync(client, $"/api/v1/transactions/{reference}", customer.Token);
        await ApiCalls.SetStatusAsync(client, await ApiCalls.OperatorTokenAsync(client), other.WalletNumber,
            new { status = "Frozen", reason = "Suspected fraud, ticket 4821" });

        var lines = sql.Api.Logs.Lines.ToList();

        foreach (var secret in new[]
                 {
                     customer.Email, customer.Phone, other.Email, other.Phone, customer.WalletNumber, other.WalletNumber,
                     reference, ApiCalls.Password, customer.Token, "Bearer", "private note about rent", "250.75", "Suspected fraud"
                 })
        {
            var found = lines.FirstOrDefault(line => line.Contains(secret, StringComparison.OrdinalIgnoreCase));
            Assert.True(found is null, $"'{secret}' found in: {found}");
        }
    }

    [Fact]
    public async Task Sign_in_and_register_log_nothing_about_who_or_what_was_sent()
    {
        var client = sql.Api.CreateClient();
        var email = ApiCalls.NewEmail();
        var phone = ApiCalls.NewPhone();

        await ApiCalls.RegisterAsync(client, ApiCalls.RegisterBody(email, phone));
        await ApiCalls.LoginAsync(client, email, "Wrong-Password-1!");
        await ApiCalls.LoginAsync(client, email, ApiCalls.Password);

        var lines = sql.Api.Logs.Lines.ToList();
        Assert.DoesNotContain(lines, line => line.Contains(email, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(lines, line => line.Contains(phone));
        Assert.DoesNotContain(lines, line => line.Contains("Wrong-Password-1!"));
        Assert.DoesNotContain(lines, line => line.Contains(ApiCalls.Password));
        Assert.NotNull(Finished("api/v1/auth/login"));
    }

    [Fact]
    public async Task Health_checks_are_not_logged_at_the_normal_level()
    {
        var client = sql.Api.CreateClient();
        var before = HealthEvents();

        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/health", TestContext.Current.CancellationToken)).StatusCode);
        }

        Assert.Equal(before, HealthEvents());
    }

    // A framework endpoint writes its pattern with a leading slash and a controller route without one.
    private int HealthEvents() =>
        sql.Api.Logs.Events.Count(logEvent => Property(logEvent, "RoutePattern")?.TrimStart('/') == "api/v1/health");

    [Fact]
    public async Task A_request_that_matches_no_route_is_still_logged_without_its_path()
    {
        var client = sql.Api.CreateClient();
        var secretPath = "/api/v1/nothing-here-" + Guid.NewGuid().ToString("N");

        var response = await client.GetAsync(secretPath, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var traceId = response.Headers.GetValues("X-Correlation-Id").Single();
        var logged = sql.Api.Logs.Events.Last(logEvent => Property(logEvent, "TraceId") == traceId && Property(logEvent, "StatusCode") == "404");
        Assert.DoesNotContain(secretPath[^12..], logged.RenderMessage());
        var leaked = sql.Api.Logs.Lines.FirstOrDefault(line => line.Contains(secretPath[^12..]));
        Assert.True(leaked is null, $"the path was logged in: {leaked}");
    }
}
