using FluentValidation;

namespace LedgerPay.Application.Admin;

public sealed class WalletStatusRequestValidator : AbstractValidator<WalletStatusRequest>
{
    public const int MinimumReasonLength = 3;
    public const int MaximumReasonLength = 250;

    public WalletStatusRequestValidator()
    {
        RuleFor(request => request.Status).NotNull().IsInEnum();

        // Both limits are counted without the spaces around the text, because the service stores the trimmed reason.
        RuleFor(request => request.Reason)
            .NotNull()
            .Must(reason => reason is not null && reason.Trim().Length >= MinimumReasonLength)
            .WithMessage($"Give a reason of at least {MinimumReasonLength} characters.")
            .Must(reason => reason is null || reason.Trim().Length <= MaximumReasonLength)
            .WithMessage($"Reason can have at most {MaximumReasonLength} characters.");
    }
}
