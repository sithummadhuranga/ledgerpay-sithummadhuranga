using LedgerPay.Application.Common;

namespace LedgerPay.UnitTests.Common;

public class ServiceResultTests
{
    [Fact]
    public void Map_changes_the_value_and_keeps_the_replayed_flag()
    {
        var mapped = ServiceResult<int>.Ok(4, replayed: true).Map(value => value.ToString());

        Assert.True(mapped.Succeeded);
        Assert.Equal("4", mapped.Value);
        Assert.True(mapped.Replayed);
    }

    [Fact]
    public void Map_keeps_a_failure_with_its_code_and_wait()
    {
        var mapped = ServiceResult<int>.FailAndWait("ACCOUNT_LOCKED", 90).Map(value => value.ToString());

        Assert.False(mapped.Succeeded);
        Assert.Equal("ACCOUNT_LOCKED", mapped.ErrorCode);
        Assert.Equal(90, mapped.RetryAfterSeconds);
        Assert.Null(mapped.Value);
    }

    [Fact]
    public void Map_does_not_run_the_conversion_for_a_failure()
    {
        var ran = false;

        ServiceResult<int>.Fail("NOT_FOUND").Map(value =>
        {
            ran = true;
            return value;
        });

        Assert.False(ran);
    }
}
