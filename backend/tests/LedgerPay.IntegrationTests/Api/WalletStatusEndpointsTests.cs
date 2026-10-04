using System.Net;
using LedgerPay.Domain.Constants;
using LedgerPay.Domain.Enums;
using LedgerPay.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace LedgerPay.IntegrationTests.Api;

public class WalletStatusEndpointsTests(SqlServerFixture sql)
{
    private HttpClient Client() => sql.Api.CreateClient();

    private static async Task<string> CodeOfAsync(HttpResponseMessage response)
    {
        using var body = await ApiCalls.ReadAsync(response);
        return body.RootElement.GetProperty("code").GetString()!;
    }

    [Fact]
    public async Task An_operator_can_freeze_a_wallet_and_the_answer_shows_the_new_status()
    {
        var client = Client();
        var customer = await ApiCalls.NewCustomerAsync(client);

        var response = await ApiCalls.SetStatusAsync(client, await ApiCalls.OperatorTokenAsync(client), customer.WalletNumber,
            new { status = "Frozen", reason = "  Checking a report  " });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await ApiCalls.ReadAsync(response);
        Assert.Equal(customer.WalletNumber, body.RootElement.GetProperty("walletNumber").GetString());
        Assert.Equal("Frozen", body.RootElement.GetProperty("status").GetString());
        Assert.Equal("Checking a report", body.RootElement.GetProperty("reason").GetString());
        await using var db = sql.NewContext();
        var wallet = await db.Wallets.AsNoTracking().SingleAsync(candidate => candidate.WalletNumber == customer.WalletNumber, TestContext.Current.CancellationToken);
        Assert.Equal(WalletStatus.Frozen, wallet.Status);
    }

    [Fact]
    public async Task An_admin_can_unfreeze_and_the_audit_entry_names_the_admin()
    {
        var client = Client();
        var customer = await ApiCalls.NewCustomerAsync(client);
        var operatorToken = await ApiCalls.OperatorTokenAsync(client);
        var adminToken = await ApiCalls.AdminTokenAsync(client);
        await ApiCalls.SetStatusAsync(client, operatorToken, customer.WalletNumber, new { status = "Frozen", reason = "Checking a report" });

        var response = await ApiCalls.SetStatusAsync(client, adminToken, customer.WalletNumber, new { status = "Active", reason = "Report closed" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var db = sql.NewContext();
        var admin = await db.Users.AsNoTracking().SingleAsync(user => user.Email == LedgerPay.Infrastructure.Seeding.SeedData.AdminEmail, TestContext.Current.CancellationToken);
        var audit = await db.AuditLogs.AsNoTracking()
            .SingleAsync(log => log.Action == AuditActions.WalletUnfrozen && log.EntityReference == customer.WalletNumber, TestContext.Current.CancellationToken);
        Assert.Equal(admin.Id, audit.ActorUserId);
    }

    [Fact]
    public async Task Setting_the_state_a_wallet_is_already_in_answers_409()
    {
        var client = Client();
        var customer = await ApiCalls.NewCustomerAsync(client);

        var response = await ApiCalls.SetStatusAsync(client, await ApiCalls.OperatorTokenAsync(client), customer.WalletNumber,
            new { status = "Active", reason = "Nothing changes" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(ErrorCodes.WalletAlreadyInState, await CodeOfAsync(response));
    }

    [Fact]
    public async Task A_wallet_that_does_not_exist_answers_404()
    {
        var client = Client();

        var response = await ApiCalls.SetStatusAsync(client, await ApiCalls.OperatorTokenAsync(client), "100000000000",
            new { status = "Frozen", reason = "Checking a report" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(ErrorCodes.WalletNotFound, await CodeOfAsync(response));
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("10000000000a")]
    [InlineData("1000000000000")]
    public async Task A_wallet_number_in_the_route_that_is_not_12_digits_answers_400_naming_it(string walletNumber)
    {
        var client = Client();

        var response = await ApiCalls.SetStatusAsync(client, await ApiCalls.OperatorTokenAsync(client), walletNumber,
            new { status = "Frozen", reason = "Checking a report" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = await ApiCalls.ReadAsync(response);
        Assert.Equal(ErrorCodes.ValidationFailed, problem.RootElement.GetProperty("code").GetString());
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty("walletNumber", out _));
    }

    public static TheoryData<string, object> BadBodies() => new()
    {
        { "reason", new { status = "Frozen", reason = "ab" } },
        { "reason", new { status = "Frozen", reason = "   " } },
        { "reason", new { status = "Frozen", reason = new string('r', 251) } },
        { "reason", new { status = "Frozen" } },
        { "status", new { reason = "Checking a report" } },
        { "status", new { status = (string?)null, reason = "Checking a report" } },
        { "status", new { status = 99, reason = "Checking a report" } },
        { "status", new { status = "Closed", reason = "Checking a report" } }
    };

    [Theory]
    [MemberData(nameof(BadBodies))]
    public async Task A_body_that_breaks_a_rule_answers_400_with_the_field_named(string field, object body)
    {
        var client = Client();
        var customer = await ApiCalls.NewCustomerAsync(client);

        var response = await ApiCalls.SetStatusAsync(client, await ApiCalls.OperatorTokenAsync(client), customer.WalletNumber, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = await ApiCalls.ReadAsync(response);
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty(field, out _), $"no error on {field}");
    }

    [Fact]
    public async Task Only_an_operator_or_an_admin_may_change_a_status()
    {
        var client = Client();
        var customer = await ApiCalls.NewCustomerAsync(client);
        var body = new { status = "Frozen", reason = "Checking a report" };

        var anonymous = await ApiCalls.SetStatusAsync(client, null!, customer.WalletNumber, body);
        var asCustomer = await ApiCalls.SetStatusAsync(client, customer.Token, customer.WalletNumber, body);

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, asCustomer.StatusCode);
        await using var db = sql.NewContext();
        var wallet = await db.Wallets.AsNoTracking().SingleAsync(candidate => candidate.WalletNumber == customer.WalletNumber, TestContext.Current.CancellationToken);
        Assert.Equal(WalletStatus.Active, wallet.Status);
    }
}
