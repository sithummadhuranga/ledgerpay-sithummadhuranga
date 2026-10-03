using LedgerPay.Domain.Constants;
using LedgerPay.Domain.Rules;

namespace LedgerPay.UnitTests.Rules;

public class LedgerSettingsTests
{
    private static Dictionary<string, decimal> AllValues() => new()
    {
        [SettingKeys.FeePercent] = 0.50m,
        [SettingKeys.FeeMinimum] = 10.00m,
        [SettingKeys.FeeMaximum] = 250.00m,
        [SettingKeys.TransferMinimum] = 100.00m,
        [SettingKeys.TransferMaximum] = 500_000.00m,
        [SettingKeys.WalletBalanceCap] = 2_000_000.00m
    };

    [Fact]
    public void Settings_are_read_from_the_stored_values()
    {
        var settings = LedgerSettings.From(AllValues());

        Assert.Equal(TestSettings.FromAssignment(), settings);
    }

    [Theory]
    [InlineData(SettingKeys.FeePercent)]
    [InlineData(SettingKeys.TransferMaximum)]
    [InlineData(SettingKeys.WalletBalanceCap)]
    public void A_missing_setting_is_named_in_the_error(string key)
    {
        var values = AllValues();
        values.Remove(key);

        var error = Assert.Throws<InvalidOperationException>(() => LedgerSettings.From(values));

        Assert.Contains(key, error.Message);
    }

    [Fact]
    public void Fee_minimum_above_the_fee_maximum_is_refused()
    {
        var values = AllValues();
        values[SettingKeys.FeeMinimum] = 300.00m;

        var error = Assert.Throws<InvalidOperationException>(() => LedgerSettings.From(values));

        Assert.Contains(SettingKeys.FeeMinimum, error.Message);
        Assert.Contains(SettingKeys.FeeMaximum, error.Message);
    }

    [Fact]
    public void Transfer_minimum_above_the_transfer_maximum_is_refused()
    {
        var values = AllValues();
        values[SettingKeys.TransferMinimum] = 600_000.00m;

        var error = Assert.Throws<InvalidOperationException>(() => LedgerSettings.From(values));

        Assert.Contains(SettingKeys.TransferMinimum, error.Message);
    }

    [Theory]
    [InlineData(SettingKeys.TransferMinimum, "0")]
    [InlineData(SettingKeys.TransferMaximum, "0")]
    [InlineData(SettingKeys.WalletBalanceCap, "0")]
    public void Limits_that_are_not_above_zero_are_refused(string key, string value)
    {
        var values = AllValues();
        values[key] = decimal.Parse(value);

        var error = Assert.Throws<InvalidOperationException>(() => LedgerSettings.From(values));

        Assert.Contains(key, error.Message);
    }

    [Fact]
    public void A_zero_fee_configuration_is_allowed()
    {
        var values = AllValues();
        values[SettingKeys.FeePercent] = 0m;
        values[SettingKeys.FeeMinimum] = 0m;
        values[SettingKeys.FeeMaximum] = 0m;

        var settings = LedgerSettings.From(values);

        Assert.Equal(0m, FeeCalculator.Calculate(1000.00m, settings));
    }
}
