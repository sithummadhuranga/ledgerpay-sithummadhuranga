using LedgerPay.Application.Idempotency;
using LedgerPay.Domain.Constants;

namespace LedgerPay.UnitTests.Idempotency;

public class IdempotencyKeyValidationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_missing_key_is_reported_as_required(string? key)
    {
        Assert.Equal(ErrorCodes.IdempotencyKeyRequired, IdempotencyService.ValidateKey(key));
    }

    [Theory]
    [InlineData("3f2b8c1e-9d4a-4b7e-8a61-2c5d7e9f0a13")]
    [InlineData("order_2026-10-04_0001")]
    [InlineData("ABCdef123")]
    [InlineData("k")]
    public void Letters_digits_and_the_two_separators_are_accepted(string key)
    {
        Assert.Null(IdempotencyService.ValidateKey(key));
    }

    [Theory]
    [InlineData("with space")]
    [InlineData("tab\there")]
    [InlineData("a/b")]
    [InlineData("a;b")]
    [InlineData("k\u00e9y")]
    [InlineData("\u0d9a\u0dca")]
    [InlineData("line\nbreak")]
    public void Any_other_character_is_a_validation_error(string key)
    {
        Assert.Equal(ErrorCodes.ValidationFailed, IdempotencyService.ValidateKey(key));
    }

    [Fact]
    public void A_key_of_the_maximum_length_is_accepted_and_one_longer_is_not()
    {
        Assert.Null(IdempotencyService.ValidateKey(new string('k', IdempotencyService.MaximumKeyLength)));
        Assert.Equal(ErrorCodes.ValidationFailed, IdempotencyService.ValidateKey(new string('k', IdempotencyService.MaximumKeyLength + 1)));
    }
}
