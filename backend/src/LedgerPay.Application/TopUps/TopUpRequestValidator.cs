using FluentValidation;
using LedgerPay.Application.Common;

namespace LedgerPay.Application.TopUps;

public sealed class TopUpRequestValidator : AbstractValidator<TopUpRequest>
{
    public TopUpRequestValidator()
    {
        RuleFor(request => request.WalletNumber).WalletNumber();
        RuleFor(request => request.Amount).MoneyAmount();
        RuleFor(request => request.BankReference).BankReference();

        RuleFor(request => request.Note).Note();
    }
}
