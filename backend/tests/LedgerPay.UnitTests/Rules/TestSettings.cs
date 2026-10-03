using LedgerPay.Domain.Rules;

namespace LedgerPay.UnitTests.Rules;

internal static class TestSettings
{
    public static LedgerSettings FromAssignment() => new(
        FeePercent: 0.50m,
        FeeMinimum: 10.00m,
        FeeMaximum: 250.00m,
        TransferMinimum: 100.00m,
        TransferMaximum: 500_000.00m,
        WalletBalanceCap: 2_000_000.00m);
}
