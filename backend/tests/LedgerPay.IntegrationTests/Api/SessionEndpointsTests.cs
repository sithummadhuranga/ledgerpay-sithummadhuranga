using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LedgerPay.Domain.Constants;
using LedgerPay.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace LedgerPay.IntegrationTests.Api;

public class SessionEndpointsTests(SqlServerFixture sql)
{
    private const string CookieName = "ledgerpay_refresh";

    private HttpClient Client() => sql.Api.CreateClient();

    private static async Task<(string Email, HttpResponseMessage Login)> SignInAsync(HttpClient client, string? email = null)
    {
        email ??= ApiCalls.NewEmail();
        (await ApiCalls.RegisterAsync(client, ApiCalls.RegisterBody(email))).EnsureSuccessStatusCode();
        var login = await ApiCalls.LoginAsync(client, email, ApiCalls.Password);
        login.EnsureSuccessStatusCode();
        return (email, login);
    }

    // The whole Set-Cookie line for the refresh cookie, or null when the response does not set it.
    private static string? SetCookie(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.FirstOrDefault(value => value.StartsWith(CookieName + "=", StringComparison.Ordinal))
            : null;

    private static string CookieValue(HttpResponseMessage response)
    {
        var line = SetCookie(response) ?? throw new InvalidOperationException("No refresh cookie was set.");
        return line[(CookieName.Length + 1)..line.IndexOf(';')];
    }

    private static bool IsCleared(HttpResponseMessage response)
    {
        var line = SetCookie(response);
        return line is not null && line.StartsWith(CookieName + "=;", StringComparison.Ordinal) && line.Contains("1970", StringComparison.Ordinal);
    }

    private static HttpRequestMessage Post(string url, string? cookie, string? fetchSite = null, string? bearer = null)
    {
        var request = ApiCalls.WithBearer(HttpMethod.Post, url, bearer);
        if (cookie is not null)
        {
            request.Headers.Add("Cookie", $"{CookieName}={cookie}");
        }

        if (fetchSite is not null)
        {
            request.Headers.Add("Sec-Fetch-Site", fetchSite);
        }

        return request;
    }

    private static Task<HttpResponseMessage> Send(HttpClient client, HttpRequestMessage request) =>
        client.SendAsync(request, TestContext.Current.CancellationToken);

    private static HttpRequestMessage SessionsRequest(HttpMethod method, string url, string? bearer, string? cookie = null, string? fetchSite = null)
    {
        var request = ApiCalls.WithBearer(method, url, bearer);
        if (cookie is not null)
        {
            request.Headers.Add("Cookie", $"{CookieName}={cookie}");
        }

        if (fetchSite is not null)
        {
            request.Headers.Add("Sec-Fetch-Site", fetchSite);
        }

        return request;
    }

    private static async Task<string> AccessTokenAsync(HttpResponseMessage response)
    {
        using var body = await ApiCalls.ReadAsync(response);
        return body.RootElement.GetProperty("accessToken").GetString()!;
    }

    // ---- sign in sets the cookie

    [Fact]
    public async Task Signing_in_sets_the_refresh_token_as_a_cookie_the_page_cannot_read_and_not_in_the_body()
    {
        var (_, login) = await SignInAsync(Client());

        var cookie = SetCookie(login)!;
        Assert.NotNull(cookie);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/v1/auth", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("expires=", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", cookie, StringComparison.OrdinalIgnoreCase);
        var text = await login.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain(CookieValue(login), text);
        Assert.DoesNotContain("refresh", text, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("no-store", login.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task A_failed_sign_in_sets_no_cookie()
    {
        var response = await ApiCalls.LoginAsync(Client(), ApiCalls.NewEmail(), "Wrong-Password-1!");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(SetCookie(response));
    }

    [Fact]
    public async Task The_cookie_is_not_marked_secure_only_where_the_host_turns_that_off_for_http()
    {
        await using var factory = new ApiFactory(sql.ApiConnectionString, TestJwt.Options(), new Dictionary<string, string?> { ["Sessions:CookieSecure"] = "false" });

        var (_, login) = await SignInAsync(factory.CreateClient());

        var cookie = SetCookie(login)!;
        Assert.DoesNotContain("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Signing_in_again_in_a_browser_ends_the_session_that_browser_held()
    {
        var client = Client();
        var (email, first) = await SignInAsync(client);
        var oldCookie = CookieValue(first);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login") { Content = JsonContent.Create(new { email, password = ApiCalls.Password }) };
        request.Headers.Add("Cookie", $"{CookieName}={oldCookie}");

        var second = await Send(client, request);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.NotEqual(oldCookie, CookieValue(second));
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(client, Post("/api/v1/auth/refresh", oldCookie))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Send(client, Post("/api/v1/auth/refresh", CookieValue(second)))).StatusCode);
    }

    [Fact]
    public async Task A_sign_in_that_fails_leaves_the_session_the_browser_held_alone()
    {
        var client = Client();
        var (email, first) = await SignInAsync(client);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login") { Content = JsonContent.Create(new { email, password = "Wrong-Password-1!" }) };
        request.Headers.Add("Cookie", $"{CookieName}={CookieValue(first)}");

        var failed = await Send(client, request);

        Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Send(client, Post("/api/v1/auth/refresh", CookieValue(first)))).StatusCode);
    }

    // ---- refresh

    [Fact]
    public async Task Refresh_with_the_cookie_gives_a_new_access_token_and_a_new_cookie()
    {
        var client = Client();
        var (_, login) = await SignInAsync(client);

        var response = await Send(client, Post("/api/v1/auth/refresh", CookieValue(login)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await ApiCalls.ReadAsync(response);
        Assert.False(string.IsNullOrEmpty(body.RootElement.GetProperty("accessToken").GetString()));
        Assert.Equal("Customer", body.RootElement.GetProperty("roles")[0].GetString());
        Assert.NotEqual(CookieValue(login), CookieValue(response));
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task The_new_access_token_works_on_a_protected_route()
    {
        var client = Client();
        var (_, login) = await SignInAsync(client);
        var refreshed = await Send(client, Post("/api/v1/auth/refresh", CookieValue(login)));

        var wallet = await client.SendAsync(
            ApiCalls.WithBearer(HttpMethod.Get, "/api/v1/wallets/me", await AccessTokenAsync(refreshed)), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, wallet.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("garbage")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnop-")]
    public async Task Refresh_without_a_working_cookie_answers_401_with_the_code_and_clears_the_cookie(string? cookie)
    {
        var response = await Send(Client(), Post("/api/v1/auth/refresh", cookie));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = await ApiCalls.ReadAsync(response);
        Assert.Equal(ErrorCodes.InvalidRefreshToken, body.RootElement.GetProperty("code").GetString());
        Assert.True(IsCleared(response));
    }

    [Fact]
    public async Task A_request_that_began_on_another_site_is_refused_and_does_not_use_up_the_cookie()
    {
        var client = Client();
        var (_, login) = await SignInAsync(client);
        var cookie = CookieValue(login);

        foreach (var site in new[] { "cross-site", "same-site" })
        {
            var refused = await Send(client, Post("/api/v1/auth/refresh", cookie, site));

            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
            using var body = await ApiCalls.ReadAsync(refused);
            Assert.Equal(ErrorCodes.Forbidden, body.RootElement.GetProperty("code").GetString());
            Assert.Null(SetCookie(refused));
        }

        Assert.Equal(HttpStatusCode.OK, (await Send(client, Post("/api/v1/auth/refresh", cookie))).StatusCode);
    }

    [Theory]
    [InlineData("same-origin")]
    [InlineData("none")]
    public async Task A_request_from_this_origin_or_typed_into_the_address_bar_is_allowed(string site)
    {
        var client = Client();
        var (_, login) = await SignInAsync(client);

        var response = await Send(client, Post("/api/v1/auth/refresh", CookieValue(login), site));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_second_use_of_a_replaced_cookie_inside_the_grace_still_answers_but_sets_no_cookie()
    {
        var client = Client();
        var (_, login) = await SignInAsync(client);
        var old = CookieValue(login);
        var first = await Send(client, Post("/api/v1/auth/refresh", old));

        var second = await Send(client, Post("/api/v1/auth/refresh", old));

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Null(SetCookie(second));
        Assert.Equal(HttpStatusCode.OK, (await Send(client, Post("/api/v1/auth/refresh", CookieValue(first)))).StatusCode);
    }

    // ---- logout

    [Fact]
    public async Task Signing_out_answers_204_clears_the_cookie_and_ends_the_session()
    {
        var client = Client();
        var (_, login) = await SignInAsync(client);
        var cookie = CookieValue(login);

        var response = await Send(client, Post("/api/v1/auth/logout", cookie));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(IsCleared(response));
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(client, Post("/api/v1/auth/refresh", cookie))).StatusCode);
    }

    [Fact]
    public async Task Signing_out_with_no_cookie_still_answers_204()
    {
        var response = await Send(Client(), Post("/api/v1/auth/logout", null));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task A_sign_out_that_began_on_another_site_is_refused_and_leaves_the_session_alone()
    {
        var client = Client();
        var (_, login) = await SignInAsync(client);
        var cookie = CookieValue(login);

        var refused = await Send(client, Post("/api/v1/auth/logout", cookie, "cross-site"));

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Send(client, Post("/api/v1/auth/refresh", cookie))).StatusCode);
    }

    // ---- the sessions list

    [Fact]
    public async Task The_sessions_list_needs_a_signed_in_user()
    {
        var response = await Send(Client(), SessionsRequest(HttpMethod.Get, "/api/v1/auth/sessions", null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task The_sessions_list_marks_this_browser_and_leaves_out_ids_and_hashes_of_tokens()
    {
        var client = Client();
        var (email, login) = await SignInAsync(client);
        await ApiCalls.LoginAsync(client, email, ApiCalls.Password);

        var response = await Send(client, SessionsRequest(HttpMethod.Get, "/api/v1/auth/sessions", await AccessTokenAsync(login), CookieValue(login)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await ApiCalls.ReadAsync(response);
        var sessions = body.RootElement.EnumerateArray().ToList();
        Assert.Equal(2, sessions.Count);
        Assert.Single(sessions, session => session.GetProperty("current").GetBoolean());
        foreach (var session in sessions)
        {
            var names = session.EnumerateObject().Select(property => property.Name).Order().ToArray();
            Assert.Equal(["current", "id", "ipAddress", "lastActiveAt", "signedInAt", "userAgent"], names);
            Assert.True(Guid.TryParse(session.GetProperty("id").GetString(), out _));
        }
    }

    [Fact]
    public async Task Nothing_in_the_list_is_marked_when_the_cookie_is_missing()
    {
        var client = Client();
        var (_, login) = await SignInAsync(client);

        var response = await Send(client, SessionsRequest(HttpMethod.Get, "/api/v1/auth/sessions", await AccessTokenAsync(login)));

        using var body = await ApiCalls.ReadAsync(response);
        Assert.All(body.RootElement.EnumerateArray(), session => Assert.False(session.GetProperty("current").GetBoolean()));
    }

    // ---- ending one

    [Fact]
    public async Task A_user_can_end_another_session_and_it_leaves_the_list()
    {
        var client = Client();
        var (email, here) = await SignInAsync(client);
        var there = await ApiCalls.LoginAsync(client, email, ApiCalls.Password);
        var bearer = await AccessTokenAsync(here);
        var list = await Send(client, SessionsRequest(HttpMethod.Get, "/api/v1/auth/sessions", bearer, CookieValue(here)));
        using var listBody = await ApiCalls.ReadAsync(list);
        var other = listBody.RootElement.EnumerateArray().Single(session => !session.GetProperty("current").GetBoolean()).GetProperty("id").GetString();

        var response = await Send(client, SessionsRequest(HttpMethod.Delete, $"/api/v1/auth/sessions/{other}", bearer, CookieValue(here)));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Null(SetCookie(response));
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(client, Post("/api/v1/auth/refresh", CookieValue(there)))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Send(client, Post("/api/v1/auth/refresh", CookieValue(here)))).StatusCode);
    }

    [Fact]
    public async Task Ending_the_current_session_clears_the_cookie()
    {
        var client = Client();
        var (_, login) = await SignInAsync(client);
        var bearer = await AccessTokenAsync(login);
        var list = await Send(client, SessionsRequest(HttpMethod.Get, "/api/v1/auth/sessions", bearer, CookieValue(login)));
        using var listBody = await ApiCalls.ReadAsync(list);
        var id = listBody.RootElement[0].GetProperty("id").GetString();

        var response = await Send(client, SessionsRequest(HttpMethod.Delete, $"/api/v1/auth/sessions/{id}", bearer, CookieValue(login)));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(IsCleared(response));
    }

    [Fact]
    public async Task A_session_of_another_user_answers_404_with_the_session_code()
    {
        var client = Client();
        var (_, victim) = await SignInAsync(client);
        var (_, intruder) = await SignInAsync(client);
        var victimList = await Send(client, SessionsRequest(HttpMethod.Get, "/api/v1/auth/sessions", await AccessTokenAsync(victim), CookieValue(victim)));
        using var victimBody = await ApiCalls.ReadAsync(victimList);
        var victimSession = victimBody.RootElement[0].GetProperty("id").GetString();

        var response = await Send(client, SessionsRequest(HttpMethod.Delete, $"/api/v1/auth/sessions/{victimSession}", await AccessTokenAsync(intruder), CookieValue(intruder)));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var body = await ApiCalls.ReadAsync(response);
        Assert.Equal(ErrorCodes.SessionNotFound, body.RootElement.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.OK, (await Send(client, Post("/api/v1/auth/refresh", CookieValue(victim)))).StatusCode);
    }

    [Fact]
    public async Task Ending_a_session_needs_a_token_and_a_request_from_this_origin()
    {
        var client = Client();
        var (_, login) = await SignInAsync(client);
        var bearer = await AccessTokenAsync(login);
        var id = Guid.NewGuid();

        var anonymous = await Send(client, SessionsRequest(HttpMethod.Delete, $"/api/v1/auth/sessions/{id}", null));
        var crossSite = await Send(client, SessionsRequest(HttpMethod.Delete, $"/api/v1/auth/sessions/{id}", bearer, fetchSite: "cross-site"));

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, crossSite.StatusCode);
    }

    // ---- audit

    [Fact]
    public async Task Refreshing_is_in_the_audit_log_with_the_address_and_the_correlation_id_and_never_the_token()
    {
        var client = Client();
        var (email, login) = await SignInAsync(client);
        var cookie = CookieValue(login);
        var request = Post("/api/v1/auth/refresh", cookie);
        var correlationId = "test-corr-" + Guid.NewGuid().ToString("N");
        request.Headers.Add("X-Correlation-Id", correlationId);

        await Send(client, request);

        await using var db = sql.NewContext();
        var userId = await db.Users.AsNoTracking().Where(user => user.Email == email).Select(user => user.Id).SingleAsync(TestContext.Current.CancellationToken);
        var rotated = await db.AuditLogs.AsNoTracking().SingleAsync(
            log => log.ActorUserId == userId && log.Action == AuditActions.RefreshRotated, TestContext.Current.CancellationToken);
        Assert.Equal(correlationId, rotated.CorrelationId);
        Assert.Equal(FakeRemoteIpStartupFilter.Address, rotated.IpAddress);
        Assert.Equal(AuditEntityTypes.Session, rotated.EntityType);
        Assert.Null(rotated.Details);
        Assert.DoesNotContain(cookie, rotated.EntityReference ?? string.Empty);
    }

    // ---- limits

    [Fact]
    public async Task Refresh_and_sign_out_share_one_allowance_per_address()
    {
        await using var factory = new ApiFactory(sql.ApiConnectionString, TestJwt.Options(), new Dictionary<string, string?>
        {
            ["RateLimits:Refresh:PermitLimit"] = "2",
            ["RateLimits:Refresh:WindowSeconds"] = "60"
        });
        var client = factory.CreateClient();

        var first = await Send(client, Post("/api/v1/auth/refresh", null));
        var second = await Send(client, Post("/api/v1/auth/logout", null));
        var third = await Send(client, Post("/api/v1/auth/refresh", null));

        Assert.Equal(HttpStatusCode.Unauthorized, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
    }
}
