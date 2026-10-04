using FluentValidation.Results;
using LedgerPay.Application.Auth;

namespace LedgerPay.UnitTests.Auth;

public class RegisterRequestValidatorTests
{
    private readonly RegisterRequestValidator validator = new();

    private ValidationResult Validate(
        string name = "Nimali Perera",
        string email = "nimali.perera@example.com",
        string phone = "+94771284635",
        string password = "Kandy-Lake-2026!") =>
        validator.Validate(new RegisterRequest(name, email, phone, password));

    private static bool HasErrorOn(ValidationResult result, string property) =>
        result.Errors.Any(error => error.PropertyName == property);

    [Fact]
    public void A_complete_request_is_valid()
    {
        Assert.True(Validate().IsValid);
    }

    [Theory]
    [InlineData("Nimali Perera")]
    [InlineData("Anne-Marie")]
    [InlineData("Conor O'Neil")]
    [InlineData("Kasun Jayawardena Mudiyanselage")]
    [InlineData("Li")]
    [InlineData("නිමලි පෙරේරා")]
    [InlineData("ප්‍රියන්ත පෙරේරා")]
    [InlineData("ශ්‍රී ලංකා")]
    [InlineData("  Nimali Perera  ")]
    public void Names_made_of_letters_with_separators_inside_are_accepted(string name)
    {
        Assert.True(Validate(name: name).IsValid);
    }

    [Theory]
    [InlineData("N")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Nimali 2")]
    [InlineData("-Nimali")]
    [InlineData("Nimali-")]
    [InlineData("'Nimali")]
    [InlineData("Nimali  Perera")]
    [InlineData("Nimali--Perera")]
    [InlineData("Nimali@Perera")]
    [InlineData("Nimali\nPerera")]
    [InlineData("ා")]
    [InlineData("\u200d\u200d")]
    [InlineData("N ")]
    [InlineData("Nimali \u200d")]
    [InlineData("Nimali\u200d")]
    public void Names_that_are_too_short_or_hold_other_characters_are_rejected(string name)
    {
        Assert.True(HasErrorOn(Validate(name: name), nameof(RegisterRequest.FullName)));
    }

    [Fact]
    public void A_name_of_100_characters_is_accepted_and_101_is_not()
    {
        Assert.True(Validate(name: new string('a', 100)).IsValid);
        Assert.True(HasErrorOn(Validate(name: new string('a', 101)), nameof(RegisterRequest.FullName)));
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing@tld")]
    [InlineData("@example.com")]
    [InlineData("two@@example.com")]
    [InlineData("spaces in@example.com")]
    [InlineData("")]
    [InlineData("nîmali@example.com")]
    [InlineData("a..b@example.com")]
    [InlineData(".a@example.com")]
    [InlineData("a.@example.com")]
    [InlineData("a@b.1")]
    [InlineData("a@-example.com")]
    [InlineData("a@example-.com")]
    [InlineData("a@example..com")]
    [InlineData("a@example.c")]
    public void Emails_that_are_not_valid_ascii_addresses_are_rejected(string email)
    {
        Assert.True(HasErrorOn(Validate(email: email), nameof(RegisterRequest.Email)));
    }

    [Fact]
    public void A_local_part_of_64_characters_is_accepted_and_65_is_not()
    {
        Assert.True(Validate(email: new string('a', 64) + "@example.com").IsValid);
        Assert.True(HasErrorOn(Validate(email: new string('a', 65) + "@example.com"), nameof(RegisterRequest.Email)));
    }

    [Theory]
    [InlineData("first.last@example.com")]
    [InlineData("user+tag@sub.example.co.lk")]
    [InlineData("o'neil@example.com")]
    [InlineData("a_b-c@exa-mple.com")]
    [InlineData(" nimali@example.com ")]
    public void Ordinary_addresses_are_accepted(string email)
    {
        Assert.True(Validate(email: email).IsValid);
    }

    [Fact]
    public void An_email_longer_than_254_characters_is_rejected()
    {
        var email = new string('a', 250) + "@example.com";

        Assert.True(HasErrorOn(Validate(email: email), nameof(RegisterRequest.Email)));
    }

    [Theory]
    [InlineData("0771284635")]
    [InlineData("+94671284635")]
    [InlineData("+9477128463")]
    [InlineData("+947712846355")]
    [InlineData("")]
    public void Phones_must_be_in_the_plus_947_format(string phone)
    {
        Assert.True(HasErrorOn(Validate(phone: phone), nameof(RegisterRequest.Phone)));
    }

    [Theory]
    [InlineData("Short1!a")]
    [InlineData("alllowercase1!")]
    [InlineData("ALLUPPERCASE1!")]
    [InlineData("NoDigitsHere!!")]
    [InlineData("NoSymbolsHere12")]
    [InlineData("")]
    public void Passwords_that_break_the_policy_are_rejected(string password)
    {
        Assert.True(HasErrorOn(Validate(password: password), nameof(RegisterRequest.Password)));
    }

    [Fact]
    public void Missing_values_are_validation_errors_and_not_crashes()
    {
        var result = validator.Validate(new RegisterRequest(null!, null!, null!, null!));

        Assert.True(HasErrorOn(result, nameof(RegisterRequest.FullName)));
        Assert.True(HasErrorOn(result, nameof(RegisterRequest.Email)));
        Assert.True(HasErrorOn(result, nameof(RegisterRequest.Phone)));
        Assert.True(HasErrorOn(result, nameof(RegisterRequest.Password)));
    }
}
