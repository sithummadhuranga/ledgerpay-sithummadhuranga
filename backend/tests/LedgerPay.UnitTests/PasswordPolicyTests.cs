using LedgerPay.Domain.Rules;

namespace LedgerPay.UnitTests;

public class PasswordPolicyTests
{
    [Theory]
    [InlineData("Kandy-Lake-2026!")]
    [InlineData("aB3$aB3$aB")]
    public void Password_with_upper_lower_digit_symbol_and_enough_length_is_accepted(string password)
    {
        Assert.True(PasswordPolicy.IsValid(password));
    }

    [Theory]
    [InlineData("Ab3$Ab3$A")]
    [InlineData("kandy-lake-2026!")]
    [InlineData("KANDY-LAKE-2026!")]
    [InlineData("Kandy-Lake-Galle!")]
    [InlineData("KandyLake20261")]
    [InlineData("")]
    public void Password_missing_a_required_part_or_too_short_is_rejected(string password)
    {
        Assert.False(PasswordPolicy.IsValid(password));
    }

    [Fact]
    public void Password_longer_than_128_characters_is_rejected()
    {
        var password = "Aa1!" + new string('x', 125);

        Assert.Equal(129, password.Length);
        Assert.False(PasswordPolicy.IsValid(password));
    }

    [Fact]
    public void Password_of_exactly_128_characters_is_accepted()
    {
        var password = "Aa1!" + new string('x', 124);

        Assert.Equal(128, password.Length);
        Assert.True(PasswordPolicy.IsValid(password));
    }
}
