using System.Net;
using System.Net.Http.Json;
using LedgerPay.Domain.Constants;
using LedgerPay.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace LedgerPay.IntegrationTests.Api;

public class TransferEndpointsTests(SqlServerFixture sql)
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
    public async Task A_transfer_by_wallet_number_answers_201_and_moves_amount_and_fee()
    {
        var client = Client();
        var sender = await ApiCalls.NewCustomerAsync(client, 5000m);
        var receiver = await ApiCalls.NewCustomerAsync(client);

        var response = await ApiCalls.TransferAsync(client, sender.Token,
            new { recipientWalletNumber = receiver.WalletNumber, amount = 1000.00m, note = "rent" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = await ApiCalls.ReadAsync(response);
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("reference").GetString()));
        Assert.Equal(1000.00m, body.RootElement.GetProperty("amount").GetDecimal());
        Assert.Equal(10.00m, body.RootElement.GetProperty("fee").GetDecimal());
        Assert.Equal(1010.00m, body.RootElement.GetProperty("total").GetDecimal());
        Assert.Equal(3990.00m, body.RootElement.GetProperty("balanceAfter").GetDecimal());
        Assert.Equal(receiver.WalletNumber, body.RootElement.GetProperty("recipientWalletNumber").GetString());
        Assert.Equal(3990.00m, await BalanceOfAsync(sender.WalletNumber));
        Assert.Equal(1000.00m, await BalanceOfAsync(receiver.WalletNumber));
        Assert.False(response.Headers.Contains("Idempotent-Replayed"));
    }

    [Fact]
    public async Task A_transfer_by_phone_number_reaches_the_same_wallet()
    {
        var client = Client();
        var sender = await ApiCalls.NewCustomerAsync(client, 2000m);
        var receiver = await ApiCalls.NewCustomerAsync(client);

        var response = await ApiCalls.TransferAsync(client, sender.Token, new { recipientPhone = receiver.Phone, amount = 500m });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(500m, await BalanceOfAsync(receiver.WalletNumber));
    }

    [Fact]
    public async Task The_sender_is_the_token_holder_whatever_the_body_says()
    {
        var client = Client();
        var sender = await ApiCalls.NewCustomerAsync(client, 3000m);
        var other = await ApiCalls.NewCustomerAsync(client, 3000m);
        var receiver = await ApiCalls.NewCustomerAsync(client);

        var response = await ApiCalls.TransferAsync(client, sender.Token, new
        {
            recipientWalletNumber = receiver.WalletNumber,
            amount = 100m,
            userId = Guid.NewGuid(),
            senderWalletNumber = other.WalletNumber,
            initiatedByUserId = Guid.NewGuid()
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(3000m, await BalanceOfAsync(other.WalletNumber));
        Assert.Equal(3000m - 100m - 10m, await BalanceOfAsync(sender.WalletNumber));
        using var body = await ApiCalls.ReadAsync(response);
        var reference = body.RootElement.GetProperty("reference").GetString();
        await using var db = sql.NewContext();
        var transaction = await db.Transactions.AsNoTracking()
            .Include(candidate => candidate.SenderWallet)
            .SingleAsync(candidate => candidate.Reference == reference, TestContext.Current.CancellationToken);
        Assert.Equal(sender.WalletNumber, transaction.SenderWallet!.WalletNumber);
        var senderUser = await db.Users.AsNoTracking().SingleAsync(user => user.Email == sender.Email, TestContext.Current.CancellationToken);
        Assert.Equal(senderUser.Id, transaction.InitiatedByUserId);
    }

    [Fact]
    public async Task A_transfer_without_an_idempotency_key_is_refused_and_moves_nothing()
    {
        var client = Client();
        var sender = await ApiCalls.NewCustomerAsync(client, 2000m);
        var receiver = await ApiCalls.NewCustomerAsync(client);

        var response = await ApiCalls.SendAsync(client, HttpMethod.Post, "/api/v1/transfers", sender.Token,
            new { recipientWalletNumber = receiver.WalletNumber, amount = 100m });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.IdempotencyKeyRequired, await CodeOfAsync(response));
        Assert.Equal(2000m, await BalanceOfAsync(sender.WalletNumber));
    }

    [Theory]
    [InlineData("has a space")]
    [InlineData("comma,inside")]
    [InlineData("café")]
    public async Task A_key_with_a_space_a_comma_or_an_accent_is_refused_and_the_header_is_named(string key)
    {
        var client = Client();
        var sender = await ApiCalls.NewCustomerAsync(client, 2000m);
        var receiver = await ApiCalls.NewCustomerAsync(client);

        var response = await ApiCalls.TransferAsync(client, sender.Token,
            new { recipientWalletNumber = receiver.WalletNumber, amount = 100m }, key);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = await ApiCalls.ReadAsync(response);
        Assert.Equal(ErrorCodes.ValidationFailed, problem.RootElement.GetProperty("code").GetString());
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty("Idempotency-Key", out _));
        Assert.Equal(2000m, await BalanceOfAsync(sender.WalletNumber));
    }

    [Fact]
    public async Task A_key_of_101_characters_is_refused_and_100_is_accepted()
    {
        var client = Client();
        var sender = await ApiCalls.NewCustomerAsync(client, 2000m);
        var receiver = await ApiCalls.NewCustomerAsync(client);
        var body = new { recipientWalletNumber = receiver.WalletNumber, amount = 100m };

        var tooLong = await ApiCalls.TransferAsync(client, sender.Token, body, new string('k', 101));
        var longest = await ApiCalls.TransferAsync(client, sender.Token, body, new string('k', 100));

        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        using var problem = await ApiCalls.ReadAsync(tooLong);
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty("Idempotency-Key", out _));
        Assert.Equal(HttpStatusCode.Created, longest.StatusCode);
    }

    [Fact]
    public async Task Two_idempotency_key_headers_are_refused()
    {
        var client = Client();
        var sender = await ApiCalls.NewCustomerAsync(client, 2000m);
        var receiver = await ApiCalls.NewCustomerAsync(client);
        var request = ApiCalls.WithBearer(HttpMethod.Post, "/api/v1/transfers", sender.Token);
        request.Content = JsonContent.Create(new { recipientWalletNumber = receiver.WalletNumber, amount = 100m });
        request.Headers.Add("Idempotency-Key", "first-key");
        request.Headers.Add("Idempotency-Key", "second-key");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(2000m, await BalanceOfAsync(sender.WalletNumber));
    }

    [Fact]
    public async Task The_same_key_and_body_gives_the_first_answer_again_and_moves_money_once()
    {
        var client = Client();
        var sender = await ApiCalls.NewCustomerAsync(client, 5000m);
        var receiver = await ApiCalls.NewCustomerAsync(client);
        var body = new { recipientWalletNumber = receiver.WalletNumber, amount = 1000m };
        var key = ApiCalls.NewKey();

        var first = await ApiCalls.TransferAsync(client, sender.Token, body, key);
        var second = await ApiCalls.TransferAsync(client, sender.Token, body, key);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.False(first.Headers.Contains("Idempotent-Replayed"));
        Assert.Equal("true", second.Headers.GetValues("Idempotent-Replayed").Single());
        using var firstBody = await ApiCalls.ReadAsync(first);
        using var secondBody = await ApiCalls.ReadAsync(second);
        Assert.Equal(firstBody.RootElement.GetProperty("reference").GetString(), secondBody.RootElement.GetProperty("reference").GetString());
        Assert.Equal(3990m, await BalanceOfAsync(sender.WalletNumber));
        Assert.Equal(1000m, await BalanceOfAsync(receiver.WalletNumber));
    }

    [Fact]
    public async Task The_same_key_with_another_amount_answers_409_and_moves_nothing_more()
    {
        var client = Client();
        var sender = await ApiCalls.NewCustomerAsync(client, 5000m);
        var receiver = await ApiCalls.NewCustomerAsync(client);
        var key = ApiCalls.NewKey();
        await ApiCalls.TransferAsync(client, sender.Token, new { recipientWalletNumber = receiver.WalletNumber, amount = 1000m }, key);

        var second = await ApiCalls.TransferAsync(client, sender.Token, new { recipientWalletNumber = receiver.WalletNumber, amount = 2000m }, key);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(ErrorCodes.IdempotencyKeyReused, await CodeOfAsync(second));
        Assert.Equal(3990m, await BalanceOfAsync(sender.WalletNumber));
    }

    [Fact]
    public async Task Ten_parallel_requests_with_one_key_move_money_once()
    {
        var client = Client();
        var sender = await ApiCalls.NewCustomerAsync(client, 5000m);
        var receiver = await ApiCalls.NewCustomerAsync(client);
        var body = new { recipientWalletNumber = receiver.WalletNumber, amount = 1000m };
        var key = ApiCalls.NewKey();

        var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => ApiCalls.TransferAsync(client, sender.Token, body, key)));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Created, response.StatusCode));
        Assert.Equal(1, responses.Count(response => !response.Headers.Contains("Idempotent-Replayed")));
        Assert.Equal(3990m, await BalanceOfAsync(sender.WalletNumber));
        Assert.Equal(1000m, await BalanceOfAsync(receiver.WalletNumber));
    }

    [Fact]
    public async Task A_rejected_transfer_is_replayed_with_the_same_rejection_under_the_same_key()
    {
        var client = Client();
        var sender = await ApiCalls.NewCustomerAsync(client, 105m);
        var receiver = await ApiCalls.NewCustomerAsync(client);
        var body = new { recipientWalletNumber = receiver.WalletNumber, amount = 100m };
        var key = ApiCalls.NewKey();

        var first = await ApiCalls.TransferAsync(client, sender.Token, body, key);
        var second = await ApiCalls.TransferAsync(client, sender.Token, body, key);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, first.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, second.StatusCode);
        Assert.Equal(ErrorCodes.InsufficientFunds, await CodeOfAsync(second));
        Assert.Equal("true", second.Headers.GetValues("Idempotent-Replayed").Single());
    }

    [Fact]
    public async Task The_balance_must_cover_the_amount_plus_the_fee()
    {
        var client = Client();
        var sender = await ApiCalls.NewCustomerAsync(client, 1005m);
        var receiver = await ApiCalls.NewCustomerAsync(client);

        var response = await ApiCalls.TransferAsync(client, sender.Token, new { recipientWalletNumber = receiver.WalletNumber, amount = 1000m });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(ErrorCodes.InsufficientFunds, await CodeOfAsync(response));
        Assert.Equal(1005m, await BalanceOfAsync(sender.WalletNumber));
        Assert.Equal(0m, await BalanceOfAsync(receiver.WalletNumber));
    }

    [Fact]
    public async Task Sending_to_your_own_wallet_is_refused()
    {
        var client = Client();
        var sender = await ApiCalls.NewCustomerAsync(client, 2000m);

        var response = await ApiCalls.TransferAsync(client, sender.Token, new { recipientWalletNumber = sender.WalletNumber, amount = 500m });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(ErrorCodes.SelfTransferNotAllowed, await CodeOfAsync(response));
    }

    [Fact]
    public async Task A_recipient_that_does_not_exist_answers_404()
    {
        var client = Client();
        var sender = await ApiCalls.NewCustomerAsync(client, 2000m);

        var response = await ApiCalls.TransferAsync(client, sender.Token, new { recipientWalletNumber = "100000000000", amount = 500m });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(ErrorCodes.RecipientNotFound, await CodeOfAsync(response));
    }

    [Theory]
    [InlineData("99.99", ErrorCodes.AmountBelowMinimum)]
    [InlineData("500000.01", ErrorCodes.AmountAboveMaximum)]
    public async Task The_limits_from_the_settings_apply(string amount, string code)
    {
        var client = Client();
        var sender = await ApiCalls.NewCustomerAsync(client, 2000m);
        var receiver = await ApiCalls.NewCustomerAsync(client);

        var response = await ApiCalls.TransferAsync(client, sender.Token, new { recipientWalletNumber = receiver.WalletNumber, amount = decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture) });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(code, await CodeOfAsync(response));
    }

    [Fact]
    public async Task A_frozen_recipient_cannot_receive_and_a_frozen_sender_cannot_send()
    {
        var client = Client();
        var operatorToken = await ApiCalls.OperatorTokenAsync(client);
        var sender = await ApiCalls.NewCustomerAsync(client, 2000m);
        var receiver = await ApiCalls.NewCustomerAsync(client);
        var body = new { recipientWalletNumber = receiver.WalletNumber, amount = 500m };

        (await ApiCalls.SetStatusAsync(client, operatorToken, receiver.WalletNumber, new { status = "Frozen", reason = "Checking a report" })).EnsureSuccessStatusCode();
        var toFrozen = await ApiCalls.TransferAsync(client, sender.Token, body);
        (await ApiCalls.SetStatusAsync(client, operatorToken, receiver.WalletNumber, new { status = "Active", reason = "Report closed" })).EnsureSuccessStatusCode();
        (await ApiCalls.SetStatusAsync(client, operatorToken, sender.WalletNumber, new { status = "Frozen", reason = "Checking a report" })).EnsureSuccessStatusCode();
        var fromFrozen = await ApiCalls.TransferAsync(client, sender.Token, body);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, toFrozen.StatusCode);
        Assert.Equal(ErrorCodes.WalletFrozen, await CodeOfAsync(toFrozen));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, fromFrozen.StatusCode);
        Assert.Equal(ErrorCodes.WalletFrozen, await CodeOfAsync(fromFrozen));
        Assert.Equal(2000m, await BalanceOfAsync(sender.WalletNumber));
    }

    public static TheoryData<string, object> BadBodies() => new()
    {
        { "amount", new { recipientWalletNumber = "100000000000", amount = 0m } },
        { "amount", new { recipientWalletNumber = "100000000000", amount = -5m } },
        { "amount", new { recipientWalletNumber = "100000000000", amount = 10.005m } },
        { "recipientWalletNumber", new { recipientWalletNumber = "12345", amount = 500m } },
        { "recipientWalletNumber", new { amount = 500m } },
        { "recipientWalletNumber", new { recipientWalletNumber = "100000000000", recipientPhone = "+94771234567", amount = 500m } },
        { "recipientPhone", new { recipientPhone = "0771234567", amount = 500m } },
        { "note", new { recipientWalletNumber = "100000000000", amount = 500m, note = new string('n', 141) } }
    };

    [Theory]
    [MemberData(nameof(BadBodies))]
    public async Task A_body_that_breaks_a_rule_answers_400_with_the_field_named(string field, object body)
    {
        var client = Client();
        var sender = await ApiCalls.NewCustomerAsync(client, 2000m);

        var response = await ApiCalls.TransferAsync(client, sender.Token, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = await ApiCalls.ReadAsync(response);
        Assert.Equal(ErrorCodes.ValidationFailed, problem.RootElement.GetProperty("code").GetString());
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty(field, out _), $"no error on {field}");
        Assert.Equal(2000m, await BalanceOfAsync(sender.WalletNumber));
    }

    [Fact]
    public async Task A_note_of_140_characters_is_accepted()
    {
        var client = Client();
        var sender = await ApiCalls.NewCustomerAsync(client, 2000m);
        var receiver = await ApiCalls.NewCustomerAsync(client);

        var response = await ApiCalls.TransferAsync(client, sender.Token,
            new { recipientWalletNumber = receiver.WalletNumber, amount = 200m, note = new string('n', 140) });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task An_empty_body_answers_400()
    {
        var client = Client();
        var sender = await ApiCalls.NewCustomerAsync(client);

        var request = ApiCalls.WithBearer(HttpMethod.Post, "/api/v1/transfers", sender.Token);
        request.Content = new StringContent(string.Empty, System.Text.Encoding.UTF8, "application/json");
        request.Headers.Add("Idempotency-Key", ApiCalls.NewKey());

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.ValidationFailed, await CodeOfAsync(response));
    }

    [Fact]
    public async Task A_body_without_a_content_type_answers_415_in_the_standard_shape()
    {
        var client = Client();
        var sender = await ApiCalls.NewCustomerAsync(client);

        var response = await ApiCalls.SendAsync(client, HttpMethod.Post, "/api/v1/transfers", sender.Token, body: null, ApiCalls.NewKey());

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.Equal(ErrorCodes.UnsupportedMediaType, await CodeOfAsync(response));
    }

    [Fact]
    public async Task Only_a_customer_may_send_money()
    {
        var client = Client();
        var receiver = await ApiCalls.NewCustomerAsync(client);
        var body = new { recipientWalletNumber = receiver.WalletNumber, amount = 500m };

        var anonymous = await ApiCalls.SendAsync(client, HttpMethod.Post, "/api/v1/transfers", null, body, ApiCalls.NewKey());
        var asOperator = await ApiCalls.TransferAsync(client, await ApiCalls.OperatorTokenAsync(client), body);
        var asAdmin = await ApiCalls.TransferAsync(client, await ApiCalls.AdminTokenAsync(client), body);

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, asOperator.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, asAdmin.StatusCode);
        Assert.Equal(ErrorCodes.Forbidden, await CodeOfAsync(asOperator));
    }

    [Theory]
    [InlineData("1000", "10.00", "1010.00")]
    [InlineData("20000", "100.00", "20100.00")]
    [InlineData("60000.00", "250.00", "60250.00")]
    [InlineData("100", "10.00", "110.00")]
    public async Task A_quote_shows_the_amount_the_fee_and_the_total(string amount, string fee, string total)
    {
        var client = Client();
        var customer = await ApiCalls.NewCustomerAsync(client);

        var response = await ApiCalls.GetAsync(client, $"/api/v1/transfers/quote?amount={amount}", customer.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await ApiCalls.ReadAsync(response);
        var invariant = System.Globalization.CultureInfo.InvariantCulture;
        Assert.Equal(decimal.Parse(amount, invariant), body.RootElement.GetProperty("amount").GetDecimal());
        Assert.Equal(decimal.Parse(fee, invariant), body.RootElement.GetProperty("fee").GetDecimal());
        Assert.Equal(decimal.Parse(total, invariant), body.RootElement.GetProperty("total").GetDecimal());
    }

    [Theory]
    [InlineData("50", 422, ErrorCodes.AmountBelowMinimum)]
    [InlineData("500000.01", 422, ErrorCodes.AmountAboveMaximum)]
    [InlineData("", 400, ErrorCodes.ValidationFailed)]
    [InlineData("0", 400, ErrorCodes.ValidationFailed)]
    [InlineData("-100", 400, ErrorCodes.ValidationFailed)]
    [InlineData("100.005", 400, ErrorCodes.ValidationFailed)]
    [InlineData("abc", 400, ErrorCodes.ValidationFailed)]
    public async Task A_quote_that_cannot_be_given_is_refused_with_a_code(string amount, int status, string code)
    {
        var client = Client();
        var customer = await ApiCalls.NewCustomerAsync(client);
        var url = amount.Length == 0 ? "/api/v1/transfers/quote" : $"/api/v1/transfers/quote?amount={amount}";

        var response = await ApiCalls.GetAsync(client, url, customer.Token);

        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal(code, await CodeOfAsync(response));
    }

    [Fact]
    public async Task A_bad_quote_amount_names_the_amount_field()
    {
        var client = Client();
        var customer = await ApiCalls.NewCustomerAsync(client);

        var response = await ApiCalls.GetAsync(client, "/api/v1/transfers/quote?amount=abc", customer.Token);

        using var problem = await ApiCalls.ReadAsync(response);
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty("amount", out _));
    }

    [Fact]
    public async Task Only_a_customer_may_ask_for_a_quote()
    {
        var client = Client();

        var anonymous = await ApiCalls.GetAsync(client, "/api/v1/transfers/quote?amount=1000", null);
        var asOperator = await ApiCalls.GetAsync(client, "/api/v1/transfers/quote?amount=1000", await ApiCalls.OperatorTokenAsync(client));

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, asOperator.StatusCode);
    }
}
