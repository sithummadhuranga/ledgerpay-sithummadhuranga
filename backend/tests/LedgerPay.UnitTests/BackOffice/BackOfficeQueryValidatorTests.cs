using LedgerPay.Application.Admin;
using LedgerPay.Application.BackOffice;
using LedgerPay.Domain.Enums;

namespace LedgerPay.UnitTests.BackOffice;

public class BackOfficeQueryValidatorTests
{
    private readonly UserSearchQueryValidator users = new();
    private readonly StaffTransactionQueryValidator transactions = new();
    private readonly AuditQueryValidator audit = new();

    [Fact]
    public void A_search_with_nothing_set_is_fine()
    {
        Assert.True(users.Validate(new UserSearchQuery()).IsValid);
        Assert.True(transactions.Validate(new StaffTransactionQuery()).IsValid);
        Assert.True(audit.Validate(new AuditQuery()).IsValid);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(-1, 20)]
    [InlineData(1_000_001, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public void Paging_outside_its_limits_is_refused_by_all_three(int page, int pageSize)
    {
        Assert.False(users.Validate(new UserSearchQuery { Page = page, PageSize = pageSize }).IsValid);
        Assert.False(transactions.Validate(new StaffTransactionQuery { Page = page, PageSize = pageSize }).IsValid);
        Assert.False(audit.Validate(new AuditQuery { Page = page, PageSize = pageSize }).IsValid);
    }

    [Fact]
    public void The_largest_page_and_page_size_are_accepted()
    {
        Assert.True(users.Validate(new UserSearchQuery { Page = 1_000_000, PageSize = 100 }).IsValid);
    }

    [Fact]
    public void A_search_text_of_100_characters_is_accepted_and_101_is_not()
    {
        Assert.True(users.Validate(new UserSearchQuery { Search = new string('a', 100) }).IsValid);
        Assert.False(users.Validate(new UserSearchQuery { Search = new string('a', 101) }).IsValid);
    }

    [Theory]
    [InlineData("482915067314", true)]
    [InlineData("48291506731", false)]
    [InlineData("4829150673144", false)]
    [InlineData("48291506731a", false)]
    [InlineData("", false)]
    public void A_wallet_number_filter_must_be_twelve_digits(string number, bool valid)
    {
        Assert.Equal(valid, transactions.Validate(new StaffTransactionQuery { WalletNumber = number }).IsValid);
    }

    [Fact]
    public void A_from_date_after_the_to_date_is_refused_for_transactions_and_for_the_audit_log()
    {
        var from = new DateOnly(2026, 10, 5);
        var to = new DateOnly(2026, 10, 4);

        Assert.False(transactions.Validate(new StaffTransactionQuery { From = from, To = to }).IsValid);
        Assert.False(audit.Validate(new AuditQuery { From = from, To = to }).IsValid);
        Assert.True(transactions.Validate(new StaffTransactionQuery { From = to, To = to }).IsValid);
    }

    [Fact]
    public void The_enum_filters_are_accepted_for_every_value()
    {
        foreach (var type in Enum.GetValues<TransactionType>())
        {
            Assert.True(transactions.Validate(new StaffTransactionQuery { Type = type }).IsValid);
        }

        foreach (var status in Enum.GetValues<TransactionStatus>())
        {
            Assert.True(transactions.Validate(new StaffTransactionQuery { Status = status }).IsValid);
        }
    }

    [Fact]
    public void An_audit_action_must_be_one_the_system_writes()
    {
        Assert.True(audit.Validate(new AuditQuery { Action = "Transfer" }).IsValid);
        Assert.False(audit.Validate(new AuditQuery { Action = "transfer" }).IsValid);
        Assert.False(audit.Validate(new AuditQuery { Action = "DropTables" }).IsValid);
        Assert.False(audit.Validate(new AuditQuery { Action = "" }).IsValid);
    }

    [Fact]
    public void An_actor_text_is_limited_to_100_characters()
    {
        Assert.True(audit.Validate(new AuditQuery { Actor = new string('a', 100) }).IsValid);
        Assert.False(audit.Validate(new AuditQuery { Actor = new string('a', 101) }).IsValid);
    }
}

public class StaffRestrictionRequestValidatorTests
{
    private readonly StaffRestrictionRequestValidator validator = new();

    private static StaffRestrictionRequest Request(string email = "dilani.senanayake@example.com", bool? restricted = true, string? reason = "Left the company") =>
        new() { Email = email, Restricted = restricted, Reason = reason };

    [Fact]
    public void A_complete_request_is_valid_for_restricting_and_for_lifting()
    {
        Assert.True(validator.Validate(Request(restricted: true)).IsValid);
        Assert.True(validator.Validate(Request(restricted: false)).IsValid);
    }

    [Fact]
    public void A_request_without_the_flag_is_refused_so_it_can_never_read_as_lift()
    {
        Assert.False(validator.Validate(Request(restricted: null)).IsValid);
    }

    [Fact]
    public void A_missing_email_is_refused_and_does_not_crash_the_validator()
    {
        var result = validator.Validate(new StaffRestrictionRequest { Email = null!, Restricted = true, Reason = "Left the company" });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(StaffRestrictionRequest.Email));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("a b@example.com")]
    public void An_email_that_is_not_one_is_refused(string email)
    {
        Assert.False(validator.Validate(Request(email: email)).IsValid);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("ab", false)]
    [InlineData("   ab   ", false)]
    [InlineData("abc", true)]
    public void The_reason_needs_three_characters_without_the_spaces_around_it(string? reason, bool valid)
    {
        Assert.Equal(valid, validator.Validate(Request(reason: reason)).IsValid);
    }

    [Fact]
    public void The_reason_is_limited_to_250_characters()
    {
        Assert.True(validator.Validate(Request(reason: new string('r', 250))).IsValid);
        Assert.False(validator.Validate(Request(reason: new string('r', 251))).IsValid);
    }
}
