using LedgerPay.Application.Transfers;

namespace LedgerPay.UnitTests.Transfers;

public class QuoteRequestValidatorTests
{
    private readonly QuoteRequestValidator validator = new();

    private static decimal Parse(string amount) => decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture);

    [Theory]
    [InlineData("100")]
    [InlineData("1000.5")]
    [InlineData("500000")]
    public void An_amount_with_at_most_two_decimals_is_accepted(string amount)
    {
        Assert.True(validator.Validate(new QuoteRequest(Parse(amount))).IsValid);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    public void An_amount_that_is_not_above_zero_is_refused(string amount)
    {
        var result = validator.Validate(new QuoteRequest(Parse(amount)));

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(QuoteRequest.Amount));
    }

    [Fact]
    public void Three_decimals_are_refused_and_not_rounded()
    {
        Assert.False(validator.Validate(new QuoteRequest(100.005m)).IsValid);
    }
}
