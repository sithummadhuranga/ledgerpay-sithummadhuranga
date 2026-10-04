using LedgerPay.Api.RateLimiting;

namespace LedgerPay.IntegrationTests.Api;

public class RateLimitOptionsTests
{
    [Fact]
    public void The_defaults_are_valid_and_limit_every_area()
    {
        var options = new RateLimitOptions();

        options.EnsureValid();

        Assert.True(options.Auth.PermitLimit > 0);
        Assert.True(options.Lookup.PermitLimit > 0);
        Assert.True(options.Money.PermitLimit > 0);
    }

    [Theory]
    [InlineData(0, 60)]
    [InlineData(-1, 60)]
    [InlineData(10, 0)]
    [InlineData(10, -5)]
    [InlineData(10, 86_401)]
    public void A_limit_or_window_out_of_range_stops_the_app_with_a_message_that_names_the_setting(int permits, int seconds)
    {
        var options = new RateLimitOptions { Lookup = new RateLimitRule { PermitLimit = permits, WindowSeconds = seconds } };

        var error = Assert.Throws<InvalidOperationException>(options.EnsureValid);

        Assert.Contains("RateLimits:Lookup", error.Message);
    }
}
