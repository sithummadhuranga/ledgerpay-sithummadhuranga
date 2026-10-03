using FluentValidation;
using LedgerPay.Application.Common;

namespace LedgerPay.Application.Transfers;

public sealed class TransferRequestValidator : AbstractValidator<TransferRequest>
{
    public const int MaximumNoteLength = 140;

    public TransferRequestValidator()
    {
        RuleFor(request => request.Amount).MoneyAmount();

        RuleFor(request => request.RecipientWalletNumber).WalletNumber()
            .When(request => request.RecipientWalletNumber is not null);

        RuleFor(request => request.RecipientPhone).MobileNumber()
            .When(request => request.RecipientPhone is not null);

        RuleFor(request => request.Note).MaximumLength(MaximumNoteLength)
            .WithMessage($"Note can have at most {MaximumNoteLength} characters.");

        RuleFor(request => request).Custom((request, context) =>
        {
            if ((request.RecipientWalletNumber is null) == (request.RecipientPhone is null))
            {
                context.AddFailure(
                    nameof(TransferRequest.RecipientWalletNumber),
                    "Give either a wallet number or a mobile number, not both and not neither.");
            }
        });
    }
}
