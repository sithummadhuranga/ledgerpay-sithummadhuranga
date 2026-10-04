using LedgerPay.Application.Wallets;

namespace LedgerPay.UnitTests.Wallets;

public class LookupQueryValidatorTests
{
    private readonly LookupQueryValidator validator = new();

    [Fact]
    public void A_wallet_number_alone_is_valid()
    {
        Assert.True(validator.Validate(new LookupQuery { WalletNumber = "123456789012" }).IsValid);
    }

    [Fact]
    public void A_mobile_number_alone_is_valid()
    {
        Assert.True(validator.Validate(new LookupQuery { Phone = "+94771234567" }).IsValid);
    }

    [Fact]
    public void Neither_is_refused()
    {
        Assert.False(validator.Validate(new LookupQuery()).IsValid);
    }

    [Fact]
    public void Both_are_refused()
    {
        Assert.False(validator.Validate(new LookupQuery { WalletNumber = "123456789012", Phone = "+94771234567" }).IsValid);
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("12345678901a")]
    [InlineData("")]
    public void A_wallet_number_that_is_not_12_digits_is_refused(string value)
    {
        var result = validator.Validate(new LookupQuery { WalletNumber = value });

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(LookupQuery.WalletNumber));
    }

    [Theory]
    [InlineData("0771234567")]
    [InlineData("+9477123456")]
    [InlineData("")]
    public void A_mobile_number_in_the_wrong_format_is_refused(string value)
    {
        var result = validator.Validate(new LookupQuery { Phone = value });

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(LookupQuery.Phone));
    }
}
