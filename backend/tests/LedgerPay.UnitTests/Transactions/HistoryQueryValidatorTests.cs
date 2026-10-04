using LedgerPay.Application.Transactions;

namespace LedgerPay.UnitTests.Transactions;

public class HistoryQueryValidatorTests
{
    private readonly HistoryQueryValidator validator = new();

    [Fact]
    public void The_defaults_are_valid()
    {
        var query = new HistoryQuery();

        Assert.Equal(1, query.Page);
        Assert.Equal(20, query.PageSize);
        Assert.True(validator.Validate(query).IsValid);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(5, 100)]
    public void A_page_from_one_and_a_size_from_1_to_100_are_valid(int page, int pageSize)
    {
        Assert.True(validator.Validate(new HistoryQuery { Page = page, PageSize = pageSize }).IsValid);
    }

    [Theory]
    [InlineData(0, 20, nameof(HistoryQuery.Page))]
    [InlineData(-1, 20, nameof(HistoryQuery.Page))]
    [InlineData(1_000_001, 20, nameof(HistoryQuery.Page))]
    [InlineData(1, 0, nameof(HistoryQuery.PageSize))]
    [InlineData(1, 101, nameof(HistoryQuery.PageSize))]
    public void A_page_or_size_out_of_range_is_refused(int page, int pageSize, string field)
    {
        var result = validator.Validate(new HistoryQuery { Page = page, PageSize = pageSize });

        Assert.Contains(result.Errors, error => error.PropertyName == field);
    }

    [Fact]
    public void A_from_date_after_the_to_date_is_refused()
    {
        var result = validator.Validate(new HistoryQuery { From = new DateOnly(2026, 10, 5), To = new DateOnly(2026, 10, 4) });

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(HistoryQuery.From));
    }

    [Fact]
    public void The_same_day_for_both_dates_is_valid()
    {
        Assert.True(validator.Validate(new HistoryQuery { From = new DateOnly(2026, 10, 4), To = new DateOnly(2026, 10, 4) }).IsValid);
    }
}
