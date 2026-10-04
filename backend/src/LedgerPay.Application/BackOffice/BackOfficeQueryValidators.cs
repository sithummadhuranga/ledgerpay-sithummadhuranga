using FluentValidation;
using LedgerPay.Application.Common;
using LedgerPay.Application.Transactions;
using LedgerPay.Domain.Constants;
using LedgerPay.Domain.Identifiers;

namespace LedgerPay.Application.BackOffice;

public sealed class UserSearchQueryValidator : AbstractValidator<UserSearchQuery>
{
    public const int MaximumSearchLength = 100;

    public UserSearchQueryValidator()
    {
        RuleFor(query => query.Page).InclusiveBetween(1, HistoryQueryValidator.MaximumPage)
            .WithMessage($"Page must be from 1 to {HistoryQueryValidator.MaximumPage}.");
        RuleFor(query => query.PageSize).InclusiveBetween(1, HistoryQueryValidator.MaximumPageSize)
            .WithMessage($"Page size must be from 1 to {HistoryQueryValidator.MaximumPageSize}.");
        RuleFor(query => query.Search).MaximumLength(MaximumSearchLength)
            .WithMessage($"Search can have at most {MaximumSearchLength} characters.");
    }
}

public sealed class StaffTransactionQueryValidator : AbstractValidator<StaffTransactionQuery>
{
    public StaffTransactionQueryValidator()
    {
        RuleFor(query => query.Page).InclusiveBetween(1, HistoryQueryValidator.MaximumPage)
            .WithMessage($"Page must be from 1 to {HistoryQueryValidator.MaximumPage}.");
        RuleFor(query => query.PageSize).InclusiveBetween(1, HistoryQueryValidator.MaximumPageSize)
            .WithMessage($"Page size must be from 1 to {HistoryQueryValidator.MaximumPageSize}.");
        RuleFor(query => query.WalletNumber).Must(number => number is not null && WalletNumbers.IsValid(number))
            .When(query => query.WalletNumber is not null)
            .WithMessage(ValidationRules.WalletNumberMessage);
        RuleFor(query => query.From).LessThanOrEqualTo(query => query.To)
            .When(query => query.From is not null && query.To is not null)
            .WithMessage("The from date must not be after the to date.");
    }
}

public sealed class AuditQueryValidator : AbstractValidator<AuditQuery>
{
    public const int MaximumActorLength = 100;

    public AuditQueryValidator()
    {
        RuleFor(query => query.Page).InclusiveBetween(1, HistoryQueryValidator.MaximumPage)
            .WithMessage($"Page must be from 1 to {HistoryQueryValidator.MaximumPage}.");
        RuleFor(query => query.PageSize).InclusiveBetween(1, HistoryQueryValidator.MaximumPageSize)
            .WithMessage($"Page size must be from 1 to {HistoryQueryValidator.MaximumPageSize}.");
        RuleFor(query => query.Action).Must(action => action is not null && AuditActions.All.Contains(action))
            .When(query => query.Action is not null)
            .WithMessage("That is not a known action.");
        RuleFor(query => query.Actor).MaximumLength(MaximumActorLength)
            .WithMessage($"Actor can have at most {MaximumActorLength} characters.");
        RuleFor(query => query.From).LessThanOrEqualTo(query => query.To)
            .When(query => query.From is not null && query.To is not null)
            .WithMessage("The from date must not be after the to date.");
    }
}
