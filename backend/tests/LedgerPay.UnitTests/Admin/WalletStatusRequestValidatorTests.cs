using LedgerPay.Application.Admin;
using LedgerPay.Domain.Enums;

namespace LedgerPay.UnitTests.Admin;

public class WalletStatusRequestValidatorTests
{
    private readonly WalletStatusRequestValidator validator = new();

    [Theory]
    [InlineData(WalletStatus.Frozen, "Suspected fraud, see ticket 4821")]
    [InlineData(WalletStatus.Active, "Customer verified by phone")]
    public void A_status_with_a_reason_is_valid(WalletStatus status, string reason)
    {
        Assert.True(validator.Validate(new WalletStatusRequest(status, reason)).IsValid);
    }

    [Fact]
    public void A_missing_status_is_rejected_and_does_not_become_active()
    {
        var result = validator.Validate(new WalletStatusRequest(null, "Valid reason"));

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(WalletStatusRequest.Status));
    }

    [Fact]
    public void An_unknown_status_value_is_rejected()
    {
        var result = validator.Validate(new WalletStatusRequest((WalletStatus)7, "Valid reason"));

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(WalletStatusRequest.Status));
    }

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    [InlineData("   ")]
    [InlineData("  a  ")]
    public void Reason_needs_at_least_three_characters_once_the_spaces_around_it_are_removed(string reason)
    {
        var result = validator.Validate(new WalletStatusRequest(WalletStatus.Frozen, reason));

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(WalletStatusRequest.Reason));
    }

    [Fact]
    public void Spaces_around_a_reason_do_not_count_towards_the_maximum()
    {
        var reason = "  " + new string('a', 250) + "   ";

        Assert.True(validator.Validate(new WalletStatusRequest(WalletStatus.Frozen, reason)).IsValid);
    }

    [Fact]
    public void Reason_of_three_characters_and_of_250_are_accepted_and_251_is_not()
    {
        Assert.True(validator.Validate(new WalletStatusRequest(WalletStatus.Frozen, "abc")).IsValid);
        Assert.True(validator.Validate(new WalletStatusRequest(WalletStatus.Frozen, new string('a', 250))).IsValid);
        Assert.False(validator.Validate(new WalletStatusRequest(WalletStatus.Frozen, new string('a', 251))).IsValid);
    }
}
