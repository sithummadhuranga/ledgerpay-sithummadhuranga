using System.Net;
using LedgerPay.Domain.Constants;
using LedgerPay.IntegrationTests.Support;

namespace LedgerPay.IntegrationTests.Api;

public class HistoryEndpointsTests(SqlServerFixture sql)
{
    private HttpClient Client() => sql.Api.CreateClient();

    private const string Url = "/api/v1/wallets/me/transactions";

    [Fact]
    public async Task History_lists_the_newest_first_with_what_the_customer_needs_to_show()
    {
        var client = Client();
        var sender = await ApiCalls.NewCustomerAsync(client, 5000m);
        var receiver = await ApiCalls.NewCustomerAsync(client);
        (await ApiCalls.TransferAsync(client, sender.Token, new { recipientWalletNumber = receiver.WalletNumber, amount = 1000m, note = "rent" })).EnsureSuccessStatusCode();

        var response = await ApiCalls.GetAsync(client, Url, sender.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await ApiCalls.ReadAsync(response);
        Assert.Equal(1, body.RootElement.GetProperty("page").GetInt32());
        Assert.Equal(20, body.RootElement.GetProperty("pageSize").GetInt32());
        Assert.Equal(2, body.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, body.RootElement.GetProperty("totalPages").GetInt32());
        var items = body.RootElement.GetProperty("items");
        Assert.Equal(2, items.GetArrayLength());
        var sent = items[0];
        Assert.Equal("Transfer", sent.GetProperty("type").GetString());
        Assert.Equal("Sent", sent.GetProperty("direction").GetString());
        Assert.Equal(1000m, sent.GetProperty("amount").GetDecimal());
        Assert.Equal(10m, sent.GetProperty("fee").GetDecimal());
        Assert.Equal(3990m, sent.GetProperty("balanceAfter").GetDecimal());
        Assert.Equal("rent", sent.GetProperty("note").GetString());
        Assert.Equal("Completed", sent.GetProperty("status").GetString());
        Assert.False(string.IsNullOrWhiteSpace(sent.GetProperty("reference").GetString()));
        Assert.EndsWith("Z", sent.GetProperty("createdAt").GetString());
        Assert.Equal("TopUp", items[1].GetProperty("type").GetString());
        Assert.Equal("Received", items[1].GetProperty("direction").GetString());
    }

    [Fact]
    public async Task History_never_shows_the_wallet_number_email_or_phone_of_the_other_side()
    {
        var client = Client();
        var sender = await ApiCalls.NewCustomerAsync(client, 5000m);
        var receiver = await ApiCalls.NewCustomerAsync(client);
        (await ApiCalls.TransferAsync(client, sender.Token, new { recipientWalletNumber = receiver.WalletNumber, amount = 1000m })).EnsureSuccessStatusCode();

        var asSender = await (await ApiCalls.GetAsync(client, Url, sender.Token)).Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var asReceiver = await (await ApiCalls.GetAsync(client, Url, receiver.Token)).Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain(receiver.WalletNumber, asSender);
        Assert.DoesNotContain(receiver.Email, asSender);
        Assert.DoesNotContain(receiver.Phone, asSender);
        Assert.DoesNotContain(sender.WalletNumber, asReceiver);
        Assert.DoesNotContain(sender.Email, asReceiver);
        Assert.DoesNotContain(sender.Phone, asReceiver);
    }

    [Fact]
    public async Task The_page_and_the_page_size_are_taken_from_the_query()
    {
        var client = Client();
        var customer = await ApiCalls.NewCustomerAsync(client);
        var operatorToken = await ApiCalls.OperatorTokenAsync(client);
        for (var i = 0; i < 3; i++)
        {
            (await ApiCalls.TopUpAsync(client, operatorToken, customer.WalletNumber, 100m)).EnsureSuccessStatusCode();
        }

        var response = await ApiCalls.GetAsync(client, $"{Url}?page=2&pageSize=2", customer.Token);

        using var body = await ApiCalls.ReadAsync(response);
        Assert.Equal(2, body.RootElement.GetProperty("page").GetInt32());
        Assert.Equal(2, body.RootElement.GetProperty("pageSize").GetInt32());
        Assert.Equal(3, body.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, body.RootElement.GetProperty("totalPages").GetInt32());
        Assert.Equal(1, body.RootElement.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task A_page_size_of_100_is_allowed()
    {
        var client = Client();
        var customer = await ApiCalls.NewCustomerAsync(client);

        var response = await ApiCalls.GetAsync(client, $"{Url}?pageSize=100", customer.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("page=0", "page")]
    [InlineData("page=-1", "page")]
    [InlineData("page=abc", "page")]
    [InlineData("pageSize=0", "pageSize")]
    [InlineData("pageSize=101", "pageSize")]
    [InlineData("pageSize=abc", "pageSize")]
    [InlineData("from=2026-10-05&to=2026-10-04", "from")]
    [InlineData("from=yesterday", "from")]
    [InlineData("to=2026-13-45", "to")]
    public async Task A_query_that_breaks_a_rule_answers_400_naming_the_field(string query, string field)
    {
        var client = Client();
        var customer = await ApiCalls.NewCustomerAsync(client);

        var response = await ApiCalls.GetAsync(client, $"{Url}?{query}", customer.Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = await ApiCalls.ReadAsync(response);
        Assert.Equal(ErrorCodes.ValidationFailed, problem.RootElement.GetProperty("code").GetString());
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty(field, out _), $"no error on {field}");
    }

    [Fact]
    public async Task The_date_filters_cut_by_the_utc_day()
    {
        var client = Client();
        var customer = await ApiCalls.NewCustomerAsync(client, 300m);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var includesToday = await ReadTotalAsync(client, customer, $"from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}");
        var endedYesterday = await ReadTotalAsync(client, customer, $"to={today.AddDays(-1):yyyy-MM-dd}");
        var startsTomorrow = await ReadTotalAsync(client, customer, $"from={today.AddDays(1):yyyy-MM-dd}");

        Assert.Equal((1, 0, 0), (includesToday, endedYesterday, startsTomorrow));
    }

    private static async Task<int> ReadTotalAsync(HttpClient client, TestCustomer customer, string query)
    {
        var response = await ApiCalls.GetAsync(client, $"{Url}?{query}", customer.Token);
        response.EnsureSuccessStatusCode();
        using var body = await ApiCalls.ReadAsync(response);
        return body.RootElement.GetProperty("totalCount").GetInt32();
    }

    [Fact]
    public async Task A_customer_never_sees_another_customers_entries()
    {
        var client = Client();
        var first = await ApiCalls.NewCustomerAsync(client, 100m);
        var second = await ApiCalls.NewCustomerAsync(client, 900m);

        var response = await ApiCalls.GetAsync(client, Url, first.Token);

        using var body = await ApiCalls.ReadAsync(response);
        var items = body.RootElement.GetProperty("items");
        Assert.Equal(1, items.GetArrayLength());
        Assert.Equal(100m, items[0].GetProperty("amount").GetDecimal());
        Assert.NotEqual(second.WalletNumber, first.WalletNumber);
    }

    [Fact]
    public async Task Only_a_customer_has_a_history()
    {
        var client = Client();

        var anonymous = await ApiCalls.GetAsync(client, Url, null);
        var asAdmin = await ApiCalls.GetAsync(client, Url, await ApiCalls.AdminTokenAsync(client));

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, asAdmin.StatusCode);
    }
}
