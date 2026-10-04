using System.Security.Cryptography;
using LedgerPay.Infrastructure.Security;

namespace LedgerPay.IntegrationTests.Auth;

public class JwtOptionsTests
{
    private static JwtOptions Valid() => new()
    {
        SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
        Issuer = "ledgerpay",
        Audience = "ledgerpay-web",
        AccessTokenMinutes = 15
    };

    [Fact]
    public void Options_with_a_32_byte_key_and_a_15_minute_lifetime_are_valid()
    {
        Valid().EnsureValid();
    }

    [Fact]
    public void A_missing_signing_key_stops_the_start_and_names_the_setting()
    {
        var options = Valid();
        options.SigningKey = "";

        var error = Assert.Throws<InvalidOperationException>(options.EnsureValid);

        Assert.Contains("Jwt:SigningKey", error.Message);
    }

    [Fact]
    public void A_key_that_is_not_base64_is_refused()
    {
        var options = Valid();
        options.SigningKey = "this is not base64 !!!";

        var error = Assert.Throws<InvalidOperationException>(options.EnsureValid);

        Assert.Contains("Jwt:SigningKey", error.Message);
    }

    [Fact]
    public void A_key_shorter_than_32_bytes_is_refused()
    {
        var options = Valid();
        options.SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(31));

        var error = Assert.Throws<InvalidOperationException>(options.EnsureValid);

        Assert.Contains("32", error.Message);
    }

    [Theory]
    [InlineData("Issuer")]
    [InlineData("Audience")]
    public void A_missing_issuer_or_audience_is_refused(string setting)
    {
        var options = Valid();
        if (setting == "Issuer")
        {
            options.Issuer = " ";
        }
        else
        {
            options.Audience = "";
        }

        var error = Assert.Throws<InvalidOperationException>(options.EnsureValid);

        Assert.Contains("Jwt:" + setting, error.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(16)]
    [InlineData(60)]
    public void A_lifetime_outside_1_to_15_minutes_is_refused(int minutes)
    {
        var options = Valid();
        options.AccessTokenMinutes = minutes;

        var error = Assert.Throws<InvalidOperationException>(options.EnsureValid);

        Assert.Contains("Jwt:AccessTokenMinutes", error.Message);
    }
}
