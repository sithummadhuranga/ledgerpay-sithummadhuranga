using LedgerPay.Domain.Rules;

namespace LedgerPay.UnitTests.Rules;

public class FeeCalculatorTests
{
    private readonly LedgerSettings settings = TestSettings.FromAssignment();

    [Theory]
    [InlineData("100.00", "10.00")]
    [InlineData("500.00", "10.00")]
    [InlineData("1999.99", "10.00")]
    public void Fee_below_the_minimum_is_raised_to_the_minimum(string amount, string expected)
    {
        Assert.Equal(decimal.Parse(expected), FeeCalculator.Calculate(decimal.Parse(amount), settings));
    }

    [Theory]
    [InlineData("2000.00", "10.00")]
    [InlineData("5000.00", "25.00")]
    [InlineData("12450.00", "62.25")]
    [InlineData("50000.00", "250.00")]
    public void Fee_in_range_is_half_a_percent_of_the_amount(string amount, string expected)
    {
        Assert.Equal(decimal.Parse(expected), FeeCalculator.Calculate(decimal.Parse(amount), settings));
    }

    [Theory]
    [InlineData("50000.01", "250.00")]
    [InlineData("60000.00", "250.00")]
    [InlineData("500000.00", "250.00")]
    public void Fee_above_the_maximum_is_lowered_to_the_maximum(string amount, string expected)
    {
        Assert.Equal(decimal.Parse(expected), FeeCalculator.Calculate(decimal.Parse(amount), settings));
    }

    [Fact]
    public void Fee_rounds_half_away_from_zero_not_to_even()
    {
        // 0.5 percent of 2001.00 is exactly 10.005. Rounding to even would give 10.00.
        Assert.Equal(10.01m, FeeCalculator.Calculate(2001.00m, settings));
    }

    [Fact]
    public void Fee_rounds_to_two_decimal_places()
    {
        // 0.5 percent of 2003.37 is 10.01685.
        Assert.Equal(10.02m, FeeCalculator.Calculate(2003.37m, settings));
    }

    [Fact]
    public void Fee_follows_the_settings_and_not_constants_in_the_code()
    {
        var stricter = settings with { FeePercent = 1.00m, FeeMinimum = 20.00m, FeeMaximum = 80.00m };

        Assert.Equal(20.00m, FeeCalculator.Calculate(1000.00m, stricter));
        Assert.Equal(50.00m, FeeCalculator.Calculate(5000.00m, stricter));
        Assert.Equal(80.00m, FeeCalculator.Calculate(9000.00m, stricter));
    }
}
