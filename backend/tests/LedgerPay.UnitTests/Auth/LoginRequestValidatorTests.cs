using LedgerPay.Application.Auth;

namespace LedgerPay.UnitTests.Auth;

public class LoginRequestValidatorTests
{
    private readonly LoginRequestValidator validator = new();

    [Fact]
    public void An_email_and_a_password_are_enough()
    {
        Assert.True(validator.Validate(new LoginRequest("nimali.perera@example.com", "whatever")).IsValid);
    }

    [Theory]
    [InlineData("", "password")]
    [InlineData("a@b.com", "")]
    [InlineData(null, "password")]
    [InlineData("a@b.com", null)]
    public void A_missing_email_or_password_is_a_validation_error(string? email, string? password)
    {
        Assert.False(validator.Validate(new LoginRequest(email!, password!)).IsValid);
    }

    [Theory]
    [InlineData("nîmali@example.com")]
    [InlineData("a b@example.com")]
    [InlineData("not-an-email")]
    public void An_email_that_could_not_have_been_registered_is_refused_so_it_cannot_fold_onto_another_one(string email)
    {
        var result = validator.Validate(new LoginRequest(email, "password"));

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(LoginRequest.Email));
    }

    [Fact]
    public void An_email_with_spaces_around_it_is_accepted_because_the_service_trims_it()
    {
        Assert.True(validator.Validate(new LoginRequest(" nimali.perera@example.com ", "password")).IsValid);
    }

    [Fact]
    public void A_password_longer_than_128_characters_is_refused_before_it_reaches_the_hasher()
    {
        var result = validator.Validate(new LoginRequest("a@b.com", new string('x', 129)));

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(LoginRequest.Password));
    }

    [Fact]
    public void The_login_does_not_judge_the_password_policy_of_an_older_password()
    {
        Assert.True(validator.Validate(new LoginRequest("a@b.com", "short")).IsValid);
    }
}
