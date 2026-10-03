namespace LedgerPay.Domain.Rules;

public static class FeeCalculator
{
    // Round first, then clamp: the minimum and maximum are whole amounts and the rounded fee is what gets charged.
    public static decimal Calculate(decimal amount, LedgerSettings settings)
    {
        var fee = Math.Round(amount * settings.FeePercent / 100m, 2, MidpointRounding.AwayFromZero);
        return Math.Clamp(fee, settings.FeeMinimum, settings.FeeMaximum);
    }
}
