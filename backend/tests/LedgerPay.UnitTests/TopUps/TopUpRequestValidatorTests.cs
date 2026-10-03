using FluentValidation.Results;
using LedgerPay.Application.TopUps;

namespace LedgerPay.UnitTests.TopUps;

public class TopUpRequestValidatorTests
{
    private readonly TopUpRequestValidator validator = new();

    private ValidationResult Validate(
        string wallet = "123456789012", decimal amount = 5000.00m, string reference = "BANK123456", string? note = null) =>
        validator.Validate(new TopUpRequest(wallet, amount, reference, note));

    [Fact]
    public void A_complete_request_is_valid()
    {
        Assert.True(Validate().IsValid);
    }

    [Theory]
    [InlineData("12345678901")]
    [InlineData("1234567890123")]
    [InlineData("12345678901A")]
    [InlineData("")]
    public void Wallet_number_must_be_exactly_twelve_digits(string wallet)
    {
        Assert.Contains(Validate(wallet: wallet).Errors, error => error.PropertyName == nameof(TopUpRequest.WalletNumber));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1.00")]
    [InlineData("10.005")]
    [InlineData("10000000000000000.00")]
    public void Amount_must_be_above_zero_with_at_most_two_decimals(string amount)
    {
        Assert.Contains(Validate(amount: decimal.Parse(amount)).Errors, error => error.PropertyName == nameof(TopUpRequest.Amount));
    }

    [Fact]
    public void A_missing_wallet_number_or_bank_reference_is_a_validation_error_and_not_a_crash()
    {
        // System.Text.Json leaves a property that is missing from the body as null, even for a non-nullable string.
        var result = validator.Validate(new TopUpRequest(null!, 100.00m, null!, null));

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(TopUpRequest.WalletNumber));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(TopUpRequest.BankReference));
    }

    [Fact]
    public void A_small_amount_is_accepted_because_top_ups_have_no_minimum()
    {
        Assert.True(Validate(amount: 0.01m).IsValid);
    }

    [Theory]
    [InlineData("ABC12")]
    [InlineData("BANK-12345")]
    [InlineData("BANK 12345")]
    [InlineData("BANK12345\n")]
    [InlineData("")]
    public void Bank_reference_must_be_six_to_forty_letters_and_digits(string reference)
    {
        Assert.Contains(Validate(reference: reference).Errors, error => error.PropertyName == nameof(TopUpRequest.BankReference));
    }

    [Theory]
    [InlineData("ABC123")]
    [InlineData("abc123")]
    public void Bank_reference_of_six_letters_and_digits_is_accepted_in_any_case(string reference)
    {
        Assert.True(Validate(reference: reference).IsValid);
    }

    [Fact]
    public void Bank_reference_of_forty_characters_is_accepted_and_forty_one_is_not()
    {
        Assert.True(Validate(reference: new string('A', 40)).IsValid);
        Assert.False(Validate(reference: new string('A', 41)).IsValid);
    }

    [Fact]
    public void Note_of_140_characters_is_accepted_and_141_is_not()
    {
        Assert.True(Validate(note: new string('a', 140)).IsValid);
        Assert.Contains(Validate(note: new string('a', 141)).Errors, error => error.PropertyName == nameof(TopUpRequest.Note));
    }
}
