using FluentValidation;
using LedgerPay.Application.Common;

namespace LedgerPay.Application.Wallets;

public sealed class LookupQueryValidator : AbstractValidator<LookupQuery>
{
    public LookupQueryValidator()
    {
        RuleFor(query => query.WalletNumber).WalletNumber().When(query => query.WalletNumber is not null);

        RuleFor(query => query.Phone).MobileNumber().When(query => query.Phone is not null);

        RuleFor(query => query).Custom((query, context) =>
        {
            if ((query.WalletNumber is null) == (query.Phone is null))
            {
                context.AddFailure(
                    nameof(LookupQuery.WalletNumber),
                    "Give either a wallet number or a mobile number, not both and not neither.");
            }
        });
    }
}
