using LedgerPay.Domain.Rules;

namespace LedgerPay.UnitTests.Rules;

public class ContactMaskTests
{
    [Theory]
    [InlineData("nimali.perera@example.com", "n***@example.com")]
    [InlineData("a@b.lk", "a***@b.lk")]
    [InlineData("first.last+tag@mail.example.co.uk", "f***@mail.example.co.uk")]
    public void An_email_shows_the_first_letter_and_the_domain(string email, string masked)
    {
        Assert.Equal(masked, ContactMask.Email(email));
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-at-sign")]
    [InlineData("@example.com")]
    public void Something_that_is_not_an_email_shows_only_stars(string value)
    {
        Assert.Equal("***", ContactMask.Email(value));
    }

    [Theory]
    [InlineData("+94771284635", "+9477***4635")]
    [InlineData("+94712390581", "+9471***0581")]
    public void A_mobile_number_shows_the_start_and_the_last_four_digits(string phone, string masked)
    {
        Assert.Equal(masked, ContactMask.Phone(phone));
    }

    [Theory]
    [InlineData("")]
    [InlineData("+94771")]
    [InlineData("123456789")]
    public void A_number_too_short_to_hide_anything_shows_only_stars(string phone)
    {
        Assert.Equal("***", ContactMask.Phone(phone));
    }

    [Fact]
    public void The_masked_values_never_hold_the_part_that_was_hidden()
    {
        Assert.DoesNotContain("imali", ContactMask.Email("nimali.perera@example.com"));
        Assert.DoesNotContain("1284", ContactMask.Phone("+94771284635").Replace("4635", ""));
    }
}
