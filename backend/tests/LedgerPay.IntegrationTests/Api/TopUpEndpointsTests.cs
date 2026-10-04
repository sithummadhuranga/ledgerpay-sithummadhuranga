using System.Net;
using LedgerPay.Domain.Constants;
using LedgerPay.Domain.Enums;
using LedgerPay.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace LedgerPay.IntegrationTests.Api;

public class TopUpEndpointsTests(SqlServerFixture sql)
{
    private HttpClient Client() => sql.Api.CreateClient();

    private async Task<decimal> BalanceOfAsync(string walletNumber)
    {
        await using var db = sql.NewContext();
        return await db.Wallets.AsNoTracking()
            .Where(wallet => wallet.WalletNumber == walletNumber)
            .Select(wallet => wallet.Balance)
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<string> CodeOfAsync(HttpResponseMessage response)
    {
        using var body = await ApiCalls.ReadAsync(response);
        return body.RootElement.GetProperty("code").GetString()!;
    }

    [Fact]
    public async Task An_operator_top_up_answers_201_and_raises_the_balance()
    {
        var client = Client();
        var customer = await ApiCalls.NewCustomerAsync(client);
        var operatorToken = await ApiCalls.OperatorTokenAsync(client);

        var response = await ApiCalls.SendAsync(client, HttpMethod.Post, "/api/v1/admin/topups", operatorToken,
            new { walletNumber = customer.WalletNumber, amount = 2500.50m, bankReference = "bank12ab34", note = "cash at branch" },
            ApiCalls.NewKey());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = await ApiCalls.ReadAsync(response);
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("reference").GetString()));
        Assert.Equal(customer.WalletNumber, body.RootElement.GetProperty("walletNumber").GetString());
        Assert.Equal(2500.50m, body.RootElement.GetProperty("amount").GetDecimal());
        Assert.Equal(2500.50m, body.RootElement.GetProperty("balanceAfter").GetDecimal());
        Assert.Equal("BANK12AB34", body.RootElement.GetProperty("bankReference").GetString());
        Assert.Equal(2500.50m, await BalanceOfAsync(customer.WalletNumber));
    }

    [Fact]
    public async Task The_operator_in_the_audit_entry_is_the_token_holder()
    {
        var client = Client();
        var customer = await ApiCalls.NewCustomerAsync(client);
        var operatorToken = await ApiCalls.OperatorTokenAsync(client);

        var response = await ApiCalls.TopUpAsync(client, operatorToken, customer.WalletNumber, 100m);

        using var body = await ApiCalls.ReadAsync(response);
        var reference = body.RootElement.GetProperty("reference").GetString();
        await using var db = sql.NewContext();
        var operatorUser = await db.Users.AsNoTracking().SingleAsync(user => user.Email == LedgerPay.Infrastructure.Seeding.SeedData.OperatorEmail, TestContext.Current.CancellationToken);
        var audit = await db.AuditLogs.AsNoTracking().SingleAsync(log => log.Action == AuditActions.TopUp && log.EntityReference == reference, TestContext.Current.CancellationToken);
        Assert.Equal(operatorUser.Id, audit.ActorUserId);
    }

    [Fact]
    public async Task The_same_key_and_body_gives_the_first_answer_again_and_credits_once()
    {
        var client = Client();
        var customer = await ApiCalls.NewCustomerAsync(client);
        var operatorToken = await ApiCalls.OperatorTokenAsync(client);
        var reference = ApiCalls.NewBankReference();
        var key = ApiCalls.NewKey();

        var first = await ApiCalls.TopUpAsync(client, operatorToken, customer.WalletNumber, 700m, reference, key);
        var second = await ApiCalls.TopUpAsync(client, operatorToken, customer.WalletNumber, 700m, reference, key);

        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal("true", second.Headers.GetValues("Idempotent-Replayed").Single());
        using var firstBody = await ApiCalls.ReadAsync(first);
        using var secondBody = await ApiCalls.ReadAsync(second);
        Assert.Equal(firstBody.RootElement.GetProperty("reference").GetString(), secondBody.RootElement.GetProperty("reference").GetString());
        Assert.Equal(700m, await BalanceOfAsync(customer.WalletNumber));
    }

    [Fact]
    public async Task The_same_key_with_another_amount_answers_409()
    {
        var client = Client();
        var customer = await ApiCalls.NewCustomerAsync(client);
        var operatorToken = await ApiCalls.OperatorTokenAsync(client);
        var reference = ApiCalls.NewBankReference();
        var key = ApiCalls.NewKey();
        await ApiCalls.TopUpAsync(client, operatorToken, customer.WalletNumber, 700m, reference, key);

        var second = await ApiCalls.TopUpAsync(client, operatorToken, customer.WalletNumber, 800m, reference, key);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(ErrorCodes.IdempotencyKeyReused, await CodeOfAsync(second));
        Assert.Equal(700m, await BalanceOfAsync(customer.WalletNumber));
    }

    [Fact]
    public async Task A_bank_reference_that_a_completed_top_up_used_is_refused_even_in_another_case()
    {
        var client = Client();
        var customer = await ApiCalls.NewCustomerAsync(client);
        var operatorToken = await ApiCalls.OperatorTokenAsync(client);
        var reference = ApiCalls.NewBankReference();
        await ApiCalls.TopUpAsync(client, operatorToken, customer.WalletNumber, 700m, reference);

        var again = await ApiCalls.TopUpAsync(client, operatorToken, customer.WalletNumber, 700m, reference.ToLowerInvariant());

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(ErrorCodes.DuplicateBankReference, await CodeOfAsync(again));
        Assert.Equal(700m, await BalanceOfAsync(customer.WalletNumber));
    }

    [Fact]
    public async Task A_top_up_without_an_idempotency_key_is_refused()
    {
        var client = Client();
        var customer = await ApiCalls.NewCustomerAsync(client);
        var operatorToken = await ApiCalls.OperatorTokenAsync(client);

        var response = await ApiCalls.SendAsync(client, HttpMethod.Post, "/api/v1/admin/topups", operatorToken,
            new { walletNumber = customer.WalletNumber, amount = 100m, bankReference = ApiCalls.NewBankReference() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.IdempotencyKeyRequired, await CodeOfAsync(response));
        Assert.Equal(0m, await BalanceOfAsync(customer.WalletNumber));
    }

    [Fact]
    public async Task A_key_with_a_space_is_refused_and_the_header_is_named()
    {
        var client = Client();
        var customer = await ApiCalls.NewCustomerAsync(client);
        var operatorToken = await ApiCalls.OperatorTokenAsync(client);

        var response = await ApiCalls.TopUpAsync(client, operatorToken, customer.WalletNumber, 100m, key: "has a space");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = await ApiCalls.ReadAsync(response);
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty("Idempotency-Key", out _));
        Assert.Equal(0m, await BalanceOfAsync(customer.WalletNumber));
    }

    public static TheoryData<string, object> BadBodies() => new()
    {
        { "walletNumber", new { walletNumber = "12345", amount = 100m, bankReference = "BANK123456" } },
        { "walletNumber", new { amount = 100m, bankReference = "BANK123456" } },
        { "amount", new { walletNumber = "100000000000", amount = 0m, bankReference = "BANK123456" } },
        { "amount", new { walletNumber = "100000000000", amount = 1.005m, bankReference = "BANK123456" } },
        { "bankReference", new { walletNumber = "100000000000", amount = 100m, bankReference = "SHORT" } },
        { "bankReference", new { walletNumber = "100000000000", amount = 100m, bankReference = "BANK-123456" } },
        { "bankReference", new { walletNumber = "100000000000", amount = 100m } },
        { "note", new { walletNumber = "100000000000", amount = 100m, bankReference = "BANK123456", note = new string('n', 141) } }
    };

    [Theory]
    [MemberData(nameof(BadBodies))]
    public async Task A_body_that_breaks_a_rule_answers_400_with_the_field_named(string field, object body)
    {
        var client = Client();
        var operatorToken = await ApiCalls.OperatorTokenAsync(client);

        var response = await ApiCalls.SendAsync(client, HttpMethod.Post, "/api/v1/admin/topups", operatorToken, body, ApiCalls.NewKey());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = await ApiCalls.ReadAsync(response);
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty(field, out _), $"no error on {field}");
    }

    [Fact]
    public async Task A_wallet_that_does_not_exist_answers_404()
    {
        var client = Client();
        var operatorToken = await ApiCalls.OperatorTokenAsync(client);

        var response = await ApiCalls.TopUpAsync(client, operatorToken, "100000000000", 100m);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(ErrorCodes.WalletNotFound, await CodeOfAsync(response));
    }

    [Fact]
    public async Task A_frozen_wallet_cannot_be_topped_up()
    {
        var client = Client();
        var customer = await ApiCalls.NewCustomerAsync(client);
        var operatorToken = await ApiCalls.OperatorTokenAsync(client);
        (await ApiCalls.SetStatusAsync(client, operatorToken, customer.WalletNumber, new { status = "Frozen", reason = "Checking a report" })).EnsureSuccessStatusCode();

        var response = await ApiCalls.TopUpAsync(client, operatorToken, customer.WalletNumber, 100m);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(ErrorCodes.WalletFrozen, await CodeOfAsync(response));
    }

    [Fact]
    public async Task A_top_up_that_would_pass_the_balance_cap_is_refused()
    {
        var client = Client();
        var customer = await ApiCalls.NewCustomerAsync(client);
        var operatorToken = await ApiCalls.OperatorTokenAsync(client);
        (await ApiCalls.TopUpAsync(client, operatorToken, customer.WalletNumber, 1_500_000m)).EnsureSuccessStatusCode();

        var response = await ApiCalls.TopUpAsync(client, operatorToken, customer.WalletNumber, 500_000.01m);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(ErrorCodes.BalanceLimitExceeded, await CodeOfAsync(response));
        Assert.Equal(1_500_000m, await BalanceOfAsync(customer.WalletNumber));
    }

    [Fact]
    public async Task Only_an_operator_may_top_up()
    {
        var client = Client();
        var customer = await ApiCalls.NewCustomerAsync(client);
        var body = new { walletNumber = customer.WalletNumber, amount = 100m, bankReference = ApiCalls.NewBankReference() };

        var anonymous = await ApiCalls.SendAsync(client, HttpMethod.Post, "/api/v1/admin/topups", null, body, ApiCalls.NewKey());
        var asCustomer = await ApiCalls.SendAsync(client, HttpMethod.Post, "/api/v1/admin/topups", customer.Token, body, ApiCalls.NewKey());
        var asAdmin = await ApiCalls.SendAsync(client, HttpMethod.Post, "/api/v1/admin/topups", await ApiCalls.AdminTokenAsync(client), body, ApiCalls.NewKey());

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, asCustomer.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, asAdmin.StatusCode);
        Assert.Equal(0m, await BalanceOfAsync(customer.WalletNumber));
    }
}
