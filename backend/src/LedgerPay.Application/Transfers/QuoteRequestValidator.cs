using FluentValidation;
using LedgerPay.Application.Common;

namespace LedgerPay.Application.Transfers;

public sealed class QuoteRequestValidator : AbstractValidator<QuoteRequest>
{
    public QuoteRequestValidator()
    {
        RuleFor(request => request.Amount).MoneyAmount();
    }
}
