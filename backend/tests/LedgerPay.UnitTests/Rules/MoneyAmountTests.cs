using LedgerPay.Domain.Rules;


namespace LedgerPay.UnitTests.Rules;

public class MoneyAmountTests
{
    [Theory]
    [InlineData("0.01")]
    [InlineData("100")]
    [InlineData("12450.50")]
    [InlineData("9999999999999999.99")]
    public void Amounts_above_zero_with_at_most_two_decimals_that_fit_the_column_are_valid(string amount)
    {
        Assert.True(MoneyAmount.IsValid(decimal.Parse(amount)));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-0.01")]
    [InlineData("10.005")]
    [InlineData("0.001")]
    [InlineData("10000000000000000.00")]
    public void Zero_negative_over_precise_and_too_large_amounts_are_not(string amount)
    {
        Assert.False(MoneyAmount.IsValid(decimal.Parse(amount)));
    }
}
