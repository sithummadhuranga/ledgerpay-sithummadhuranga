using LedgerPay.Application.Transactions;

namespace LedgerPay.UnitTests.Transactions;

public class PagedResponseTests
{
    [Theory]
    [InlineData(0, 20, 0)]
    [InlineData(1, 20, 1)]
    [InlineData(20, 20, 1)]
    [InlineData(21, 20, 2)]
    [InlineData(41, 20, 3)]
    [InlineData(100, 1, 100)]
    public void The_page_count_rounds_up(int total, int pageSize, int expectedPages)
    {
        var response = PagedResponse.Create(new List<int>(), 1, pageSize, total);

        Assert.Equal(expectedPages, response.TotalPages);
        Assert.Equal(total, response.TotalCount);
    }
}
