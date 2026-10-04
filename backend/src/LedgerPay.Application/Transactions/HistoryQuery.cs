namespace LedgerPay.Application.Transactions;

// From and To are whole days in UTC, and both are included.
public sealed class HistoryQuery
{
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;

    public DateOnly? From { get; set; }

    public DateOnly? To { get; set; }
}
