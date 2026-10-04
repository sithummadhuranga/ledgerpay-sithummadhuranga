using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LedgerPay.Domain.Constants;
using LedgerPay.Infrastructure.Seeding;
using LedgerPay.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace LedgerPay.IntegrationTests.Api;

public class BackOfficeEndpointsTests(SqlServerFixture sql)
{
    private HttpClient Client() => sql.Api.CreateClient();

    private static async Task<string> CodeAsync(HttpResponseMessage response)
    {
        using var body = await ApiCalls.ReadAsync(response);
        return body.RootElement.GetProperty("code").GetString()!;
    }

    // ---- who may call what

    [Fact]
    public async Task Nobody_who_is_not_signed_in_can_read_any_back_office_route()
    {
        var client = Client();

        foreach (var url in new[] { "/api/v1/admin/users", "/api/v1/admin/users/482915067314", "/api/v1/admin/transactions", "/api/v1/admin/audit-logs", "/api/v1/admin/staff" })
        {
            var response = await ApiCalls.GetAsync(client, url, null);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public async Task A_customer_is_refused_every_back_office_route_with_403()
    {
        var client = Client();
        var customer = await ApiCalls.NewCustomerAsync(client);

        foreach (var url in new[] { "/api/v1/admin/users", $"/api/v1/admin/users/{customer.WalletNumber}", "/api/v1/admin/transactions", "/api/v1/admin/audit-logs", "/api/v1/admin/staff" })
        {
            var response = await ApiCalls.GetAsync(client, url, customer.Token);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal(ErrorCodes.Forbidden, await CodeAsync(response));
        }

        var restrict = await ApiCalls.SendAsync(client, HttpMethod.Patch, "/api/v1/admin/staff/restriction", customer.Token,
            new { email = SeedData_OperatorEmail, restricted = true, reason = "Because" });
        Assert.Equal(HttpStatusCode.Forbidden, restrict.StatusCode);
    }

    private const string SeedData_OperatorEmail = "dilani.senanayake@example.com";

    [Fact]
    public async Task An_operator_reads_users_and_transactions_but_not_the_audit_log_or_the_staff_list()
    {
        var client = Client();
        var token = await ApiCalls.OperatorTokenAsync(client);

        Assert.Equal(HttpStatusCode.OK, (await ApiCalls.GetAsync(client, "/api/v1/admin/users", token)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ApiCalls.GetAsync(client, "/api/v1/admin/transactions", token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ApiCalls.GetAsync(client, "/api/v1/admin/audit-logs", token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ApiCalls.GetAsync(client, "/api/v1/admin/staff", token)).StatusCode);
    }

    [Fact]
    public async Task An_operator_cannot_restrict_anyone()
    {
        var client = Client();
        var token = await ApiCalls.OperatorTokenAsync(client);

        var response = await ApiCalls.SendAsync(client, HttpMethod.Patch, "/api/v1/admin/staff/restriction", token,
            new { email = SeedData_OperatorEmail, restricted = true, reason = "Because" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task An_admin_reads_everything_in_the_back_office()
    {
        var client = Client();
        var token = await ApiCalls.AdminTokenAsync(client);

        foreach (var url in new[] { "/api/v1/admin/users", "/api/v1/admin/transactions", "/api/v1/admin/audit-logs", "/api/v1/admin/staff" })
        {
            var response = await ApiCalls.GetAsync(client, url, token);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        }
    }

    // ---- finding people

    [Fact]
    public async Task The_user_list_finds_a_customer_by_email_and_has_the_agreed_fields_only()
    {
        var client = Client();
        var customer = await ApiCalls.NewCustomerAsync(client);
        var token = await ApiCalls.OperatorTokenAsync(client);

        var response = await ApiCalls.GetAsync(client, $"/api/v1/admin/users?search={Uri.EscapeDataString(customer.Email)}", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await ApiCalls.ReadAsync(response);
        var item = Assert.Single(body.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(["balance", "createdAt", "email", "fullName", "locked", "phone", "walletNumber", "walletStatus"],
            item.EnumerateObject().Select(property => property.Name).Order().ToArray());
        Assert.Equal(customer.WalletNumber, item.GetProperty("walletNumber").GetString());
        Assert.Equal("Active", item.GetProperty("walletStatus").GetString());
        Assert.Equal((1, 20, 1, 1), (body.RootElement.GetProperty("page").GetInt32(), body.RootElement.GetProperty("pageSize").GetInt32(),
            body.RootElement.GetProperty("totalCount").GetInt32(), body.RootElement.GetProperty("totalPages").GetInt32()));
    }

    [Fact]
    public async Task The_status_filter_works_through_the_query_string_and_a_bad_value_is_a_400()
    {
        var client = Client();
        var customer = await ApiCalls.NewCustomerAsync(client);
        var operatorToken = await ApiCalls.OperatorTokenAsync(client);
        (await ApiCalls.SetStatusAsync(client, operatorToken, customer.WalletNumber, new { status = "Frozen", reason = "Reported lost phone" })).EnsureSuccessStatusCode();

        var frozen = await ApiCalls.GetAsync(client, $"/api/v1/admin/users?search={customer.WalletNumber}&status=Frozen", operatorToken);
        var active = await ApiCalls.GetAsync(client, $"/api/v1/admin/users?search={customer.WalletNumber}&status=Active", operatorToken);
        var bogus = await ApiCalls.GetAsync(client, "/api/v1/admin/users?status=Bogus", operatorToken);

        using var frozenBody = await ApiCalls.ReadAsync(frozen);
        using var activeBody = await ApiCalls.ReadAsync(active);
        Assert.Equal(1, frozenBody.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(0, activeBody.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, bogus.StatusCode);
        Assert.Equal(ErrorCodes.ValidationFailed, await CodeAsync(bogus));
    }

    [Theory]
    [InlineData("/api/v1/admin/users?pageSize=101", "pageSize")]
    [InlineData("/api/v1/admin/users?page=0", "page")]
    [InlineData("/api/v1/admin/transactions?walletNumber=abc", "walletNumber")]
    [InlineData("/api/v1/admin/transactions?type=Nope", "type")]
    [InlineData("/api/v1/admin/transactions?from=2026-10-05&to=2026-10-04", "from")]
    public async Task Bad_filters_are_a_400_that_names_the_field(string url, string field)
    {
        var client = Client();
        var token = await ApiCalls.OperatorTokenAsync(client);

        var response = await ApiCalls.GetAsync(client, url, token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = await ApiCalls.ReadAsync(response);
        Assert.Equal(ErrorCodes.ValidationFailed, body.RootElement.GetProperty("code").GetString());
        Assert.True(body.RootElement.GetProperty("errors").TryGetProperty(field, out _), $"no error for {field}");
    }

    [Fact]
    public async Task An_audit_filter_with_an_action_that_does_not_exist_is_a_400()
    {
        var client = Client();
        var response = await ApiCalls.GetAsync(client, "/api/v1/admin/audit-logs?action=DropTables", await ApiCalls.AdminTokenAsync(client));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---- one person

    [Fact]
    public async Task A_person_page_shows_their_wallet_and_their_transactions_and_is_audited()
    {
        var client = Client();
        var customer = await ApiCalls.NewCustomerAsync(client, fundedWith: 5000m);
        var token = await ApiCalls.AdminTokenAsync(client);
        var correlationId = "test-corr-" + Guid.NewGuid().ToString("N");
        var request = ApiCalls.WithBearer(HttpMethod.Get, $"/api/v1/admin/users/{customer.WalletNumber}", token);
        request.Headers.Add("X-Correlation-Id", correlationId);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await ApiCalls.ReadAsync(response);
        var root = body.RootElement;
        Assert.Equal((customer.Email, customer.Phone, 5000m, "Active"), (root.GetProperty("email").GetString(), root.GetProperty("phone").GetString(),
            root.GetProperty("balance").GetDecimal(), root.GetProperty("walletStatus").GetString()));
        var recent = Assert.Single(root.GetProperty("recentTransactions").EnumerateArray());
        Assert.Equal("TopUp", recent.GetProperty("type").GetString());
        Assert.Equal(customer.WalletNumber, recent.GetProperty("receiverWalletNumber").GetString());
        Assert.False(root.TryGetProperty("passwordHash", out _));
        Assert.False(root.TryGetProperty("id", out _));
        await using var db = sql.NewContext();
        Assert.True(await db.AuditLogs.AnyAsync(
            log => log.CorrelationId == correlationId && log.Action == AuditActions.UserViewed && log.EntityReference == customer.WalletNumber,
            TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("48291506731")]
    public async Task A_wallet_number_that_is_not_twelve_digits_is_a_400(string walletNumber)
    {
        var client = Client();

        var response = await ApiCalls.GetAsync(client, $"/api/v1/admin/users/{walletNumber}", await ApiCalls.OperatorTokenAsync(client));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_wallet_that_does_not_exist_is_a_404()
    {
        var client = Client();

        var response = await ApiCalls.GetAsync(client, "/api/v1/admin/users/100000000007", await ApiCalls.OperatorTokenAsync(client));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(ErrorCodes.WalletNotFound, await CodeAsync(response));
    }

    // ---- the transactions list and the audit log

    [Fact]
    public async Task The_transaction_list_filters_by_wallet_and_status()
    {
        var client = Client();
        var customer = await ApiCalls.NewCustomerAsync(client, fundedWith: 1000m);
        var token = await ApiCalls.OperatorTokenAsync(client);

        var all = await ApiCalls.GetAsync(client, $"/api/v1/admin/transactions?walletNumber={customer.WalletNumber}", token);
        var failedOnly = await ApiCalls.GetAsync(client, $"/api/v1/admin/transactions?walletNumber={customer.WalletNumber}&status=Failed", token);

        using var allBody = await ApiCalls.ReadAsync(all);
        using var failedBody = await ApiCalls.ReadAsync(failedOnly);
        Assert.Equal(1, allBody.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(0, failedBody.RootElement.GetProperty("totalCount").GetInt32());
        var item = allBody.RootElement.GetProperty("items")[0];
        Assert.Equal("Completed", item.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("senderWalletNumber").ValueKind);
    }

    [Fact]
    public async Task The_audit_log_shows_a_top_up_with_the_operator_who_made_it()
    {
        var client = Client();
        var customer = await ApiCalls.NewCustomerAsync(client, fundedWith: 1000m);
        var token = await ApiCalls.AdminTokenAsync(client);

        var response = await ApiCalls.GetAsync(client, $"/api/v1/admin/audit-logs?action=TopUp&actor={Uri.EscapeDataString(SeedData_OperatorEmail)}&pageSize=100", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await ApiCalls.ReadAsync(response);
        var items = body.RootElement.GetProperty("items").EnumerateArray().ToList();
        Assert.NotEmpty(items);
        Assert.All(items, item => Assert.Equal(("TopUp", SeedData_OperatorEmail), (item.GetProperty("action").GetString(), item.GetProperty("actorEmail").GetString())));
        Assert.NotEmpty(customer.WalletNumber);
    }

    // ---- restricting an operator

    private async Task<(string Email, string Password)> NewOperatorAsync()
    {
        const string password = "Kandy-Lake-2026!";
        await using var db = sql.NewContext();
        var user = await TestData.AddStaffAsync(db, RoleNames.Operator, password);
        return (user.Email, password);
    }

    private static Task<HttpResponseMessage> RestrictAsync(HttpClient client, string adminToken, string email, bool? restricted, string? reason = "Left the company")
    {
        object body = restricted is null ? new { email, reason } : new { email, restricted, reason };
        return ApiCalls.SendAsync(client, HttpMethod.Patch, "/api/v1/admin/staff/restriction", adminToken, body);
    }

    [Fact]
    public async Task A_restricted_operator_loses_access_at_once_even_with_a_token_that_has_not_run_out()
    {
        var client = Client();
        var (email, password) = await NewOperatorAsync();
        var operatorToken = await ApiCalls.TokenAsync(client, email, password);
        var admin = await ApiCalls.AdminTokenAsync(client);
        Assert.Equal(HttpStatusCode.OK, (await ApiCalls.GetAsync(client, "/api/v1/admin/users", operatorToken)).StatusCode);

        var restricted = await RestrictAsync(client, admin, email, true);

        Assert.Equal(HttpStatusCode.OK, restricted.StatusCode);
        var afterwards = await ApiCalls.GetAsync(client, "/api/v1/admin/users", operatorToken);
        Assert.Equal(HttpStatusCode.Unauthorized, afterwards.StatusCode);
        Assert.Equal(ErrorCodes.Unauthenticated, await CodeAsync(afterwards));
    }

    [Fact]
    public async Task A_restricted_operator_cannot_sign_in_and_is_told_why_only_with_the_right_password()
    {
        var client = Client();
        var (email, password) = await NewOperatorAsync();
        await RestrictAsync(client, await ApiCalls.AdminTokenAsync(client), email, true);

        var right = await ApiCalls.LoginAsync(client, email, password);
        var wrong = await ApiCalls.LoginAsync(client, email, "Wrong-Password-1!");

        Assert.Equal(HttpStatusCode.Forbidden, right.StatusCode);
        Assert.Equal(ErrorCodes.AccountRestricted, await CodeAsync(right));
        Assert.Null(right.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies.FirstOrDefault(value => value.StartsWith("ledgerpay_refresh=", StringComparison.Ordinal)) : null);
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal(ErrorCodes.InvalidCredentials, await CodeAsync(wrong));
    }

    [Fact]
    public async Task The_refresh_cookie_of_a_restricted_operator_stops_working()
    {
        var client = Client();
        var (email, password) = await NewOperatorAsync();
        var login = await ApiCalls.LoginAsync(client, email, password);
        var cookie = login.Headers.GetValues("Set-Cookie").First(value => value.StartsWith("ledgerpay_refresh=", StringComparison.Ordinal)).Split(';')[0];
        await RestrictAsync(client, await ApiCalls.AdminTokenAsync(client), email, true);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        request.Headers.Add("Cookie", cookie);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Lifting_the_restriction_lets_the_operator_sign_in_again_and_the_list_shows_both_states()
    {
        var client = Client();
        var (email, password) = await NewOperatorAsync();
        var admin = await ApiCalls.AdminTokenAsync(client);

        await RestrictAsync(client, admin, email, true, "Suspected misuse");
        var whileRestricted = await ApiCalls.GetAsync(client, "/api/v1/admin/staff", admin);
        await RestrictAsync(client, admin, email, false, "Cleared after review");
        var afterwards = await ApiCalls.GetAsync(client, "/api/v1/admin/staff", admin);

        using var restrictedBody = await ApiCalls.ReadAsync(whileRestricted);
        using var liftedBody = await ApiCalls.ReadAsync(afterwards);
        var restrictedItem = restrictedBody.RootElement.EnumerateArray().Single(item => item.GetProperty("email").GetString() == email);
        var liftedItem = liftedBody.RootElement.EnumerateArray().Single(item => item.GetProperty("email").GetString() == email);
        Assert.True(restrictedItem.GetProperty("restricted").GetBoolean());
        Assert.Equal("Suspected misuse", restrictedItem.GetProperty("restrictedReason").GetString());
        Assert.Equal("Chamara Rajapaksa", restrictedItem.GetProperty("restrictedBy").GetString());
        Assert.False(liftedItem.GetProperty("restricted").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await ApiCalls.LoginAsync(client, email, password)).StatusCode);
    }

    [Fact]
    public async Task The_restriction_route_refuses_what_it_should_with_the_right_codes()
    {
        var client = Client();
        var (email, _) = await NewOperatorAsync();
        var admin = await ApiCalls.AdminTokenAsync(client);
        var customer = await ApiCalls.NewCustomerAsync(client);
        await RestrictAsync(client, admin, email, true);

        var twice = await RestrictAsync(client, admin, email, true);
        var ofAdmin = await RestrictAsync(client, admin, "chamara.rajapaksa@example.com", true);
        var ofCustomer = await RestrictAsync(client, admin, customer.Email, true);
        var ofNobody = await RestrictAsync(client, admin, "nobody.here@example.com", true);
        var noFlag = await RestrictAsync(client, admin, email, null);
        var shortReason = await RestrictAsync(client, admin, email, false, "no");
        var badEmail = await RestrictAsync(client, admin, "not-an-email", true);

        Assert.Equal((HttpStatusCode.Conflict, ErrorCodes.AccountAlreadyInState), (twice.StatusCode, await CodeAsync(twice)));
        Assert.Equal((HttpStatusCode.UnprocessableEntity, ErrorCodes.StaffNotRestrictable), (ofAdmin.StatusCode, await CodeAsync(ofAdmin)));
        Assert.Equal((HttpStatusCode.NotFound, ErrorCodes.StaffNotFound), (ofCustomer.StatusCode, await CodeAsync(ofCustomer)));
        Assert.Equal((HttpStatusCode.NotFound, ErrorCodes.StaffNotFound), (ofNobody.StatusCode, await CodeAsync(ofNobody)));
        Assert.Equal(HttpStatusCode.BadRequest, noFlag.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, shortReason.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badEmail.StatusCode);
    }

    // ---- limits

    [Fact]
    public async Task The_back_office_reads_share_one_allowance_for_each_staff_member()
    {
        await using var factory = new ApiFactory(sql.ApiConnectionString, TestJwt.Options(), new Dictionary<string, string?>
        {
            ["RateLimits:BackOffice:PermitLimit"] = "3",
            ["RateLimits:BackOffice:WindowSeconds"] = "60"
        });
        var client = factory.CreateClient();
        var token = await ApiCalls.OperatorTokenAsync(client);

        var statuses = new List<HttpStatusCode>();
        foreach (var url in new[] { "/api/v1/admin/users", "/api/v1/admin/transactions", "/api/v1/admin/users", "/api/v1/admin/transactions" })
        {
            statuses.Add((await ApiCalls.GetAsync(client, url, token)).StatusCode);
        }

        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests], statuses);
    }
}
