using System.Net;
using LedgerPay.Domain.Constants;
using LedgerPay.Domain.Enums;
using LedgerPay.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace LedgerPay.IntegrationTests.Api;

public class TransactionEndpointsTests(SqlServerFixture sql)
{
    private HttpClient Client() => sql.Api.CreateClient();

    private static string Url(string reference) => $"/api/v1/transactions/{reference}";

    private static async Task<string> TransferAsync(HttpClient client, TestCustomer from, TestCustomer to, decimal amount)
    {
        var response = await ApiCalls.TransferAsync(client, from.Token, new { recipientWalletNumber = to.WalletNumber, amount, note = "rent" });
        response.EnsureSuccessStatusCode();
        using var body = await ApiCalls.ReadAsync(response);
        return body.RootElement.GetProperty("reference").GetString()!;
    }

    private static async Task<string> CodeOfAsync(HttpResponseMessage response)
    {
        using var body = await ApiCalls.ReadAsync(response);
        return body.RootElement.GetProperty("code").GetString()!;
    }

    [Fact]
    public async Task The_sender_sees_a_sent_transfer_with_the_fee_and_a_masked_receiver()
    {
        var client = Client();
        var sender = await ApiCalls.NewCustomerAsync(client, 5000m);
        var receiver = await ApiCalls.NewCustomerAsync(client);
        var reference = await TransferAsync(client, sender, receiver, 1000m);

        var response = await ApiCalls.GetAsync(client, Url(reference), sender.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await ApiCalls.ReadAsync(response);
        Assert.Equal(reference, body.RootElement.GetProperty("reference").GetString());
        Assert.Equal("Transfer", body.RootElement.GetProperty("type").GetString());
        Assert.Equal("Completed", body.RootElement.GetProperty("status").GetString());
        Assert.Equal("Sent", body.RootElement.GetProperty("direction").GetString());
        Assert.Equal(1000m, body.RootElement.GetProperty("amount").GetDecimal());
        Assert.Equal(10m, body.RootElement.GetProperty("fee").GetDecimal());
        Assert.Equal("N*** P***", body.RootElement.GetProperty("counterpartyName").GetString());
        Assert.Equal("rent", body.RootElement.GetProperty("note").GetString());
    }

    [Fact]
    public async Task The_receiver_sees_it_as_received_without_the_fee()
    {
        var client = Client();
        var sender = await ApiCalls.NewCustomerAsync(client, 5000m);
        var receiver = await ApiCalls.NewCustomerAsync(client);
        var reference = await TransferAsync(client, sender, receiver, 1000m);

        var response = await ApiCalls.GetAsync(client, Url(reference), receiver.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await ApiCalls.ReadAsync(response);
        Assert.Equal("Received", body.RootElement.GetProperty("direction").GetString());
        Assert.Equal(0m, body.RootElement.GetProperty("fee").GetDecimal());
    }

    [Fact]
    public async Task A_customer_who_is_not_in_the_transaction_gets_404_with_the_same_answer_as_for_a_reference_that_does_not_exist()
    {
        var client = Client();
        var sender = await ApiCalls.NewCustomerAsync(client, 5000m);
        var receiver = await ApiCalls.NewCustomerAsync(client);
        var stranger = await ApiCalls.NewCustomerAsync(client);
        var reference = await TransferAsync(client, sender, receiver, 1000m);

        var someoneElses = await ApiCalls.GetAsync(client, Url(reference), stranger.Token);
        var unknown = await ApiCalls.GetAsync(client, Url("TX00000000000000"), stranger.Token);

        Assert.Equal(HttpStatusCode.NotFound, someoneElses.StatusCode);
        Assert.Equal(ErrorCodes.TransactionNotFound, await CodeOfAsync(someoneElses));
        Assert.Equal(unknown.StatusCode, someoneElses.StatusCode);
        Assert.Equal(await ApiCalls.WithoutTraceIdAsync(unknown), await ApiCalls.WithoutTraceIdAsync(someoneElses));
    }

    [Fact]
    public async Task A_top_up_is_visible_to_the_wallet_owner_without_the_bank_reference()
    {
        var client = Client();
        var customer = await ApiCalls.NewCustomerAsync(client);
        var topUp = await ApiCalls.TopUpAsync(client, await ApiCalls.OperatorTokenAsync(client), customer.WalletNumber, 700m, "BANKSECRET99");
        using var created = await ApiCalls.ReadAsync(topUp);
        var reference = created.RootElement.GetProperty("reference").GetString()!;

        var response = await ApiCalls.GetAsync(client, Url(reference), customer.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("TopUp", text);
        Assert.DoesNotContain("BANKSECRET99", text);
    }

    [Theory]
    [InlineData("Operator")]
    [InlineData("Admin")]
    public async Task The_back_office_sees_any_transaction_with_both_wallet_numbers(string role)
    {
        var client = Client();
        var sender = await ApiCalls.NewCustomerAsync(client, 5000m);
        var receiver = await ApiCalls.NewCustomerAsync(client);
        var reference = await TransferAsync(client, sender, receiver, 1000m);
        var token = role == "Admin" ? await ApiCalls.AdminTokenAsync(client) : await ApiCalls.OperatorTokenAsync(client);

        var response = await ApiCalls.GetAsync(client, Url(reference), token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await ApiCalls.ReadAsync(response);
        Assert.Equal(sender.WalletNumber, body.RootElement.GetProperty("senderWalletNumber").GetString());
        Assert.Equal(receiver.WalletNumber, body.RootElement.GetProperty("receiverWalletNumber").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, body.RootElement.GetProperty("direction").ValueKind);
    }

    [Fact]
    public async Task The_back_office_sees_the_bank_reference_of_a_top_up()
    {
        var client = Client();
        var customer = await ApiCalls.NewCustomerAsync(client);
        var operatorToken = await ApiCalls.OperatorTokenAsync(client);
        var topUp = await ApiCalls.TopUpAsync(client, operatorToken, customer.WalletNumber, 700m, "BANKVISIBLE1");
        using var created = await ApiCalls.ReadAsync(topUp);

        var response = await ApiCalls.GetAsync(client, Url(created.RootElement.GetProperty("reference").GetString()!), operatorToken);

        using var body = await ApiCalls.ReadAsync(response);
        Assert.Equal("BANKVISIBLE1", body.RootElement.GetProperty("bankReference").GetString());
    }

    [Fact]
    public async Task A_failed_transfer_is_visible_to_who_sent_it_and_the_back_office_but_not_to_the_receiver()
    {
        var client = Client();
        var sender = await ApiCalls.NewCustomerAsync(client, 105m);
        var receiver = await ApiCalls.NewCustomerAsync(client);
        var rejected = await ApiCalls.TransferAsync(client, sender.Token, new { recipientWalletNumber = receiver.WalletNumber, amount = 1000m });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, rejected.StatusCode);
        await using var db = sql.NewContext();
        var failed = await db.Transactions.AsNoTracking()
            .Where(transaction => transaction.Status == TransactionStatus.Failed && transaction.SenderWallet!.WalletNumber == sender.WalletNumber)
            .SingleAsync(TestContext.Current.CancellationToken);

        var asSender = await ApiCalls.GetAsync(client, Url(failed.Reference), sender.Token);
        var asReceiver = await ApiCalls.GetAsync(client, Url(failed.Reference), receiver.Token);
        var asOperator = await ApiCalls.GetAsync(client, Url(failed.Reference), await ApiCalls.OperatorTokenAsync(client));

        Assert.Equal(HttpStatusCode.OK, asSender.StatusCode);
        using var body = await ApiCalls.ReadAsync(asSender);
        Assert.Equal("Failed", body.RootElement.GetProperty("status").GetString());
        Assert.Equal(ErrorCodes.InsufficientFunds, body.RootElement.GetProperty("failureCode").GetString());
        Assert.Equal(HttpStatusCode.NotFound, asReceiver.StatusCode);
        Assert.Equal(HttpStatusCode.OK, asOperator.StatusCode);
    }

    [Theory]
    [InlineData("tx00000000000000")]
    [InlineData("TX")]
    [InlineData("%20")]
    [InlineData("TX0000000000000%27%20OR%201=1")]
    public async Task A_reference_that_matches_nothing_answers_404(string reference)
    {
        var client = Client();
        var customer = await ApiCalls.NewCustomerAsync(client);

        var response = await ApiCalls.GetAsync(client, Url(reference), customer.Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_very_long_reference_answers_404_and_not_500()
    {
        var client = Client();
        var customer = await ApiCalls.NewCustomerAsync(client);

        var response = await ApiCalls.GetAsync(client, Url(new string('A', 600)), customer.Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(ErrorCodes.TransactionNotFound, await CodeOfAsync(response));
    }

    [Fact]
    public async Task Anyone_signed_in_may_ask_but_an_anonymous_caller_may_not()
    {
        var client = Client();

        var anonymous = await ApiCalls.GetAsync(client, Url("TX00000000000000"), null);

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }
}
