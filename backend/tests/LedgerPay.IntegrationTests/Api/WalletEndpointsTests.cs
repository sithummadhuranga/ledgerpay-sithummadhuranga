using System.Net;
using LedgerPay.Domain.Constants;
using LedgerPay.IntegrationTests.Support;

namespace LedgerPay.IntegrationTests.Api;

public class WalletEndpointsTests(SqlServerFixture sql)
{
    private HttpClient Client() => sql.Api.CreateClient();

    private static async Task<string> CodeOfAsync(HttpResponseMessage response)
    {
        using var body = await ApiCalls.ReadAsync(response);
        return body.RootElement.GetProperty("code").GetString()!;
    }

    [Fact]
    public async Task My_wallet_shows_the_number_the_holder_the_balance_and_the_status()
    {
        var client = Client();
        var customer = await ApiCalls.NewCustomerAsync(client, 2500.50m);

        var response = await ApiCalls.GetAsync(client, "/api/v1/wallets/me", customer.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await ApiCalls.ReadAsync(response);
        Assert.Equal(customer.WalletNumber, body.RootElement.GetProperty("walletNumber").GetString());
        Assert.Equal("Nimali Perera", body.RootElement.GetProperty("holderName").GetString());
        Assert.Equal(2500.50m, body.RootElement.GetProperty("balance").GetDecimal());
        Assert.Equal(2500.50m, body.RootElement.GetProperty("availableBalance").GetDecimal());
        Assert.Equal("LKR", body.RootElement.GetProperty("currency").GetString());
        Assert.Equal("Active", body.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task A_new_wallet_has_a_zero_balance()
    {
        var client = Client();
        var customer = await ApiCalls.NewCustomerAsync(client);

        var response = await ApiCalls.GetAsync(client, "/api/v1/wallets/me", customer.Token);

        using var body = await ApiCalls.ReadAsync(response);
        Assert.Equal(0m, body.RootElement.GetProperty("balance").GetDecimal());
    }

    [Fact]
    public async Task My_wallet_shows_a_freeze_but_never_the_reason_for_it()
    {
        var client = Client();
        var customer = await ApiCalls.NewCustomerAsync(client);
        (await ApiCalls.SetStatusAsync(client, await ApiCalls.OperatorTokenAsync(client), customer.WalletNumber,
            new { status = "Frozen", reason = "Suspected fraud, ticket 4821" })).EnsureSuccessStatusCode();

        var response = await ApiCalls.GetAsync(client, "/api/v1/wallets/me", customer.Token);

        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Frozen", text);
        Assert.DoesNotContain("fraud", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("4821", text);
        Assert.DoesNotContain("reason", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Each_customer_sees_only_their_own_wallet()
    {
        var client = Client();
        var first = await ApiCalls.NewCustomerAsync(client, 100m);
        var second = await ApiCalls.NewCustomerAsync(client, 900m);

        var firstView = await ApiCalls.ReadAsync(await ApiCalls.GetAsync(client, "/api/v1/wallets/me", first.Token));
        var secondView = await ApiCalls.ReadAsync(await ApiCalls.GetAsync(client, "/api/v1/wallets/me", second.Token));

        Assert.Equal(first.WalletNumber, firstView.RootElement.GetProperty("walletNumber").GetString());
        Assert.Equal(second.WalletNumber, secondView.RootElement.GetProperty("walletNumber").GetString());
        Assert.Equal(900m, secondView.RootElement.GetProperty("balance").GetDecimal());
    }

    [Fact]
    public async Task Only_a_customer_has_a_wallet_to_look_at()
    {
        var client = Client();

        var anonymous = await ApiCalls.GetAsync(client, "/api/v1/wallets/me", null);
        var asOperator = await ApiCalls.GetAsync(client, "/api/v1/wallets/me", await ApiCalls.OperatorTokenAsync(client));

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, asOperator.StatusCode);
    }

    [Fact]
    public async Task A_lookup_by_wallet_number_gives_the_number_a_masked_name_and_the_active_flag_only()
    {
        var client = Client();
        var asker = await ApiCalls.NewCustomerAsync(client);
        var target = await ApiCalls.NewCustomerAsync(client);

        var response = await ApiCalls.GetAsync(client, $"/api/v1/wallets/lookup?walletNumber={target.WalletNumber}", asker.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await ApiCalls.ReadAsync(response);
        Assert.Equal(["active", "holderName", "walletNumber"], body.RootElement.EnumerateObject().Select(property => property.Name).Order());
        Assert.Equal(target.WalletNumber, body.RootElement.GetProperty("walletNumber").GetString());
        Assert.Equal("N*** P***", body.RootElement.GetProperty("holderName").GetString());
        Assert.True(body.RootElement.GetProperty("active").GetBoolean());
    }

    [Fact]
    public async Task A_lookup_by_phone_finds_the_same_wallet()
    {
        var client = Client();
        var asker = await ApiCalls.NewCustomerAsync(client);
        var target = await ApiCalls.NewCustomerAsync(client);

        var response = await ApiCalls.GetAsync(client, $"/api/v1/wallets/lookup?phone={Uri.EscapeDataString(target.Phone)}", asker.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await ApiCalls.ReadAsync(response);
        Assert.Equal(target.WalletNumber, body.RootElement.GetProperty("walletNumber").GetString());
    }

    [Fact]
    public async Task A_lookup_never_shows_the_email_the_phone_the_balance_or_the_full_name()
    {
        var client = Client();
        var asker = await ApiCalls.NewCustomerAsync(client);
        var target = await ApiCalls.NewCustomerAsync(client, 612.45m);

        var response = await ApiCalls.GetAsync(client, $"/api/v1/wallets/lookup?walletNumber={target.WalletNumber}", asker.Token);

        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain(target.Email, text);
        Assert.DoesNotContain(target.Phone, text);
        Assert.DoesNotContain("612.45", text);
        Assert.DoesNotContain("Nimali", text);
        Assert.DoesNotContain("Perera", text);
    }

    [Fact]
    public async Task A_frozen_wallet_is_found_but_not_active()
    {
        var client = Client();
        var asker = await ApiCalls.NewCustomerAsync(client);
        var target = await ApiCalls.NewCustomerAsync(client);
        (await ApiCalls.SetStatusAsync(client, await ApiCalls.OperatorTokenAsync(client), target.WalletNumber,
            new { status = "Frozen", reason = "Checking a report" })).EnsureSuccessStatusCode();

        var response = await ApiCalls.GetAsync(client, $"/api/v1/wallets/lookup?walletNumber={target.WalletNumber}", asker.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await ApiCalls.ReadAsync(response);
        Assert.False(body.RootElement.GetProperty("active").GetBoolean());
    }

    [Theory]
    [InlineData("walletNumber=100000000000")]
    [InlineData("phone=%2B94700000000")]
    public async Task A_wallet_that_does_not_exist_answers_404(string query)
    {
        var client = Client();
        var asker = await ApiCalls.NewCustomerAsync(client);

        var response = await ApiCalls.GetAsync(client, $"/api/v1/wallets/lookup?{query}", asker.Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(ErrorCodes.WalletNotFound, await CodeOfAsync(response));
    }

    [Theory]
    [InlineData("", "walletNumber")]
    [InlineData("walletNumber=100000000000&phone=%2B94771234567", "walletNumber")]
    [InlineData("walletNumber=12345", "walletNumber")]
    [InlineData("walletNumber=", "walletNumber")]
    [InlineData("phone=0771234567", "phone")]
    [InlineData("phone=", "walletNumber")]
    public async Task A_lookup_with_both_neither_or_a_bad_value_answers_400_naming_the_field(string query, string field)
    {
        var client = Client();
        var asker = await ApiCalls.NewCustomerAsync(client);

        var response = await ApiCalls.GetAsync(client, $"/api/v1/wallets/lookup?{query}", asker.Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = await ApiCalls.ReadAsync(response);
        Assert.Equal(ErrorCodes.ValidationFailed, problem.RootElement.GetProperty("code").GetString());
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty(field, out _), $"no error on {field}");
    }

    [Fact]
    public async Task Only_a_customer_may_look_a_wallet_up()
    {
        var client = Client();
        var target = await ApiCalls.NewCustomerAsync(client);
        var url = $"/api/v1/wallets/lookup?walletNumber={target.WalletNumber}";

        var anonymous = await ApiCalls.GetAsync(client, url, null);
        var asOperator = await ApiCalls.GetAsync(client, url, await ApiCalls.OperatorTokenAsync(client));

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, asOperator.StatusCode);
    }
}
