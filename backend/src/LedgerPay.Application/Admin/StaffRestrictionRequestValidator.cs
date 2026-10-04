using FluentValidation;
using LedgerPay.Application.Common;

namespace LedgerPay.Application.Admin;

public sealed class StaffRestrictionRequestValidator : AbstractValidator<StaffRestrictionRequest>
{
    public StaffRestrictionRequestValidator()
    {
        RuleFor(request => request.Email).EmailAddress();
        RuleFor(request => request.Restricted).NotNull().WithMessage("Say whether the account is restricted.");

        // Counted without the spaces around the text, because the service stores the trimmed reason.
        RuleFor(request => request.Reason)
            .NotNull()
            .Must(reason => reason is not null && reason.Trim().Length >= WalletStatusRequestValidator.MinimumReasonLength)
            .WithMessage($"Give a reason of at least {WalletStatusRequestValidator.MinimumReasonLength} characters.")
            .Must(reason => reason is null || reason.Trim().Length <= WalletStatusRequestValidator.MaximumReasonLength)
            .WithMessage($"Reason can have at most {WalletStatusRequestValidator.MaximumReasonLength} characters.");
    }
}
