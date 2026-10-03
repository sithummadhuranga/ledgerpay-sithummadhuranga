using LedgerPay.Domain.Entities;
using LedgerPay.Domain.Rules;

namespace LedgerPay.UnitTests.Rules;

public class AvailableBalanceTests
{
    [Fact]
    public void Available_balance_equals_the_balance_while_there_are_no_holds()
    {
        var wallet = new Wallet { Balance = 12450.00m };

        Assert.Equal(12450.00m, AvailableBalance.Of(wallet));
    }

    [Theory]
    [InlineData("1010.00", "1000.00", "10.00", true)]
    [InlineData("1009.99", "1000.00", "10.00", false)]
    [InlineData("1000.00", "1000.00", "10.00", false)]
    [InlineData("0.00", "100.00", "10.00", false)]
    public void Available_balance_must_cover_the_amount_plus_the_fee(string balance, string amount, string fee, bool covered)
    {
        var wallet = new Wallet { Balance = decimal.Parse(balance) };

        Assert.Equal(covered, AvailableBalance.Covers(wallet, decimal.Parse(amount), decimal.Parse(fee)));
    }
}
