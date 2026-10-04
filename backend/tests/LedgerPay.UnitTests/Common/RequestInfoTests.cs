using LedgerPay.Application.Common;

namespace LedgerPay.UnitTests.Common;

public class RequestInfoTests
{
    [Fact]
    public void Values_that_fit_the_audit_columns_are_kept()
    {
        var info = new RequestInfo("203.0.113.7", "corr-123");

        Assert.Equal("203.0.113.7", info.IpAddress);
        Assert.Equal("corr-123", info.CorrelationId);
    }

    [Fact]
    public void Long_values_are_cut_to_the_audit_column_sizes()
    {
        var info = new RequestInfo(new string('1', 200), new string('c', 200));

        Assert.Equal(RequestInfo.IpAddressLength, info.IpAddress!.Length);
        Assert.Equal(RequestInfo.CorrelationIdLength, info.CorrelationId!.Length);
    }

    [Fact]
    public void Missing_values_stay_missing()
    {
        var info = new RequestInfo(null, null);

        Assert.Null(info.IpAddress);
        Assert.Null(info.CorrelationId);
    }
}
