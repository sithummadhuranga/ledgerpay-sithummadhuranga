using System.Buffers.Text;
using LedgerPay.Domain.Rules;

namespace LedgerPay.UnitTests.Rules;

public class SessionRulesTests
{
    private static readonly DateTime Start = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void A_session_that_is_used_lasts_seven_days_from_that_use()
    {
        var now = Start.AddDays(3);

        Assert.Equal(now.AddDays(7), SessionRules.ExpiresAt(now, Start));
    }

    [Fact]
    public void A_session_never_lasts_past_thirty_days_from_its_sign_in()
    {
        var now = Start.AddDays(28);

        Assert.Equal(Start.AddDays(30), SessionRules.ExpiresAt(now, Start));
    }

    [Fact]
    public void At_the_edge_the_two_limits_agree()
    {
        var now = Start.AddDays(23);

        Assert.Equal(Start.AddDays(30), SessionRules.ExpiresAt(now, Start));
    }

    [Fact]
    public void The_token_length_is_what_thirty_two_bytes_make_in_base64url()
    {
        Assert.Equal(SessionRules.TokenLength, Base64Url.EncodeToString(new byte[SessionRules.TokenBytes]).Length);
    }

    [Theory]
    [InlineData("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnop-")]
    [InlineData("0123456789_0123456789_0123456789_0123456789")]
    public void A_token_of_the_right_length_in_base64url_characters_is_well_formed(string token)
    {
        Assert.Equal(SessionRules.TokenLength, token.Length);
        Assert.True(SessionRules.IsWellFormed(token));
    }

    // Forty-two valid characters. One more makes a token, so each case below differs from a token in one way only.
    private const string FortyTwo = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnop";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short")]
    [InlineData(FortyTwo)]
    [InlineData(FortyTwo + "-" + "q")]
    [InlineData(FortyTwo + "+")]
    [InlineData(FortyTwo + "/")]
    [InlineData(FortyTwo + "=")]
    [InlineData(FortyTwo + " ")]
    [InlineData(FortyTwo + "\u00e9")]
    public void Anything_else_is_refused_before_the_database_is_asked(string? token)
    {
        Assert.False(SessionRules.IsWellFormed(token));
    }
}
