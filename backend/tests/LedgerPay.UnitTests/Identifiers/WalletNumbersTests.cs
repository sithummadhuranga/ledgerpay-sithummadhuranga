using LedgerPay.Domain.Identifiers;

namespace LedgerPay.UnitTests.Identifiers;

public class WalletNumbersTests
{
    [Theory]
    [InlineData("100000000000")]
    [InlineData("987654321098")]
    public void Twelve_ascii_digits_are_a_wallet_number(string value)
    {
        Assert.True(WalletNumbers.IsValid(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12345678901")]
    [InlineData("1234567890123")]
    [InlineData("10000000000a")]
    [InlineData(" 10000000000")]
    [InlineData("100000000000\n")]
    [InlineData("١٢٣٤٥٦٧٨٩٠١٢")]
    public void Anything_else_is_not(string? value)
    {
        Assert.False(WalletNumbers.IsValid(value));
    }

    [Fact]
    public void A_generated_number_is_always_valid()
    {
        for (var i = 0; i < 200; i++)
        {
            Assert.True(WalletNumbers.IsValid(WalletNumberGenerator.Next()));
        }
    }
}
