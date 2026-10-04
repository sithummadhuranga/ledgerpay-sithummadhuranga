using FluentValidation.Results;
using LedgerPay.Application.Transfers;

namespace LedgerPay.UnitTests.Transfers;

public class TransferRequestValidatorTests
{
    private readonly TransferRequestValidator validator = new();

    private ValidationResult Validate(
        string? walletNumber = "123456789012", string? phone = null, decimal amount = 1000.00m, string? note = null) =>
        validator.Validate(new TransferRequest(walletNumber, phone, amount, note));

    [Fact]
    public void Request_with_a_wallet_number_is_valid()
    {
        Assert.True(Validate().IsValid);
    }

    [Fact]
    public void Request_with_a_phone_number_is_valid()
    {
        Assert.True(Validate(walletNumber: null, phone: "+94771234567").IsValid);
    }

    [Fact]
    public void Request_without_a_recipient_is_rejected()
    {
        var result = Validate(walletNumber: null, phone: null);

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(TransferRequest.RecipientWalletNumber));
    }

    [Fact]
    public void Request_with_both_recipient_fields_is_rejected()
    {
        var result = Validate(walletNumber: "123456789012", phone: "+94771234567");

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("12345678901")]
    [InlineData("1234567890123")]
    [InlineData("12345678901A")]
    [InlineData("")]
    public void Wallet_number_must_be_exactly_twelve_digits(string walletNumber)
    {
        var result = Validate(walletNumber: walletNumber);

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(TransferRequest.RecipientWalletNumber));
    }

    [Theory]
    [InlineData("0771234567")]
    [InlineData("+9477123456")]
    [InlineData("+947712345678")]
    [InlineData("+94671234567")]
    [InlineData("94771234567")]
    public void Phone_must_be_in_the_plus_947_format(string phone)
    {
        var result = Validate(walletNumber: null, phone: phone);

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(TransferRequest.RecipientPhone));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5.00")]
    [InlineData("100.005")]
    [InlineData("0.001")]
    [InlineData("10000000000000000.00")]
    public void Amount_must_be_above_zero_with_at_most_two_decimals_and_fit_the_column(string amount)
    {
        var result = Validate(amount: decimal.Parse(amount));

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(TransferRequest.Amount));
    }

    [Theory]
    [InlineData("0.01")]
    [InlineData("100")]
    [InlineData("12450.50")]
    [InlineData("9999999999999999.99")]
    public void Amount_with_up_to_two_decimals_is_accepted(string amount)
    {
        Assert.True(Validate(amount: decimal.Parse(amount)).IsValid);
    }

    [Fact]
    public void Note_of_140_characters_is_accepted_and_141_is_not()
    {
        Assert.True(Validate(note: new string('a', 140)).IsValid);

        var result = Validate(note: new string('a', 141));

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(TransferRequest.Note));
    }

    [Fact]
    public void Missing_note_is_accepted()
    {
        Assert.True(Validate(note: null).IsValid);
    }
}
