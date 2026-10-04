using FluentValidation;
using LedgerPay.Application.Common;

namespace LedgerPay.Application.Auth;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        // A value that is missing from the body arrives as null. Stop at the first failed rule so later rules never see it.
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(request => request.FullName).FullName();
        RuleFor(request => request.Email).EmailAddress();
        RuleFor(request => request.Phone).MobileNumber();
        RuleFor(request => request.Password).NewPassword();
    }
}
