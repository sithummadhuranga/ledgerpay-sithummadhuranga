using FluentValidation;

namespace LedgerPay.Application.Transactions;

public sealed class HistoryQueryValidator : AbstractValidator<HistoryQuery>
{
    public const int MaximumPageSize = 100;

    // Keeps (page - 1) * pageSize far inside an int.
    public const int MaximumPage = 1_000_000;

    public HistoryQueryValidator()
    {
        RuleFor(query => query.Page).InclusiveBetween(1, MaximumPage)
            .WithMessage($"Page must be from 1 to {MaximumPage}.");

        RuleFor(query => query.PageSize).InclusiveBetween(1, MaximumPageSize)
            .WithMessage($"Page size must be from 1 to {MaximumPageSize}.");

        RuleFor(query => query.From).LessThanOrEqualTo(query => query.To)
            .When(query => query.From is not null && query.To is not null)
            .WithMessage("The from date must not be after the to date.");
    }
}
