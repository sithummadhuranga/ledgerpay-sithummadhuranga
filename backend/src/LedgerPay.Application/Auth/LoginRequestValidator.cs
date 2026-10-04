using FluentValidation;
using LedgerPay.Application.Common;
using LedgerPay.Domain.Rules;

namespace LedgerPay.Application.Auth;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        // The same check as registration, so an email that could never have been registered is refused here too.
        RuleFor(request => request.Email).EmailAddress();

        // Only the length is checked. An older password may not meet today's policy, and a very long one must not reach the hasher.
        RuleFor(request => request.Password).NotEmpty().MaximumLength(PasswordPolicy.MaximumLength);
    }
}
