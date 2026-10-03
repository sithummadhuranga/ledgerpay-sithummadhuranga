using LedgerPay.Domain.Identifiers;

namespace LedgerPay.UnitTests.Identifiers;

public class WalletNumberGeneratorTests
{
    [Fact]
    public void Wallet_number_has_exactly_twelve_digits()
    {
        for (var i = 0; i < 1000; i++)
        {
            var number = WalletNumberGenerator.Next();

            Assert.Equal(12, number.Length);
            Assert.All(number, digit => Assert.InRange(digit, '0', '9'));
        }
    }

    [Fact]
    public void Wallet_number_never_starts_with_zero()
    {
        for (var i = 0; i < 1000; i++)
        {
            Assert.NotEqual('0', WalletNumberGenerator.Next()[0]);
        }
    }

    [Fact]
    public void Wallet_numbers_are_not_sequential()
    {
        var numbers = Enumerable.Range(0, 200).Select(_ => WalletNumberGenerator.Next()).ToList();

        Assert.Equal(numbers.Count, numbers.Distinct().Count());
        Assert.NotEqual(numbers.Order().ToList(), numbers);
    }
}
