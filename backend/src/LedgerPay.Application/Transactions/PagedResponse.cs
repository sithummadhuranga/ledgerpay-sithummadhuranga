namespace LedgerPay.Application.Transactions;

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount, int TotalPages);

public static class PagedResponse
{
    public static PagedResponse<T> Create<T>(IReadOnlyList<T> items, int page, int pageSize, int totalCount) =>
        new(items, page, pageSize, totalCount, (totalCount + pageSize - 1) / pageSize);
}
