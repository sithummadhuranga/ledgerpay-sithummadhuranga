using LedgerPay.Domain.Constants;

namespace LedgerPay.Domain.Rules;

// The fee and the limits as they are stored in SystemSettings. Nothing here is a constant in the code.
public sealed record LedgerSettings(
    decimal FeePercent,
    decimal FeeMinimum,
    decimal FeeMaximum,
    decimal TransferMinimum,
    decimal TransferMaximum,
    decimal WalletBalanceCap)
{
    public static LedgerSettings From(IReadOnlyDictionary<string, decimal> values) => EnsureConsistent(new LedgerSettings(
        FeePercent: Require(values, SettingKeys.FeePercent),
        FeeMinimum: Require(values, SettingKeys.FeeMinimum),
        FeeMaximum: Require(values, SettingKeys.FeeMaximum),
        TransferMinimum: Require(values, SettingKeys.TransferMinimum),
        TransferMaximum: Require(values, SettingKeys.TransferMaximum),
        WalletBalanceCap: Require(values, SettingKeys.WalletBalanceCap)));

    // The settings are edited in the database. A bad value stops here with a clear message,
    // instead of surfacing later as an exception from Math.Clamp or as a limit that allows anything.
    private static LedgerSettings EnsureConsistent(LedgerSettings settings)
    {
        if (settings.FeePercent < 0)
        {
            throw Invalid($"{SettingKeys.FeePercent} must not be negative.");
        }

        if (settings.FeeMinimum < 0 || settings.FeeMinimum > settings.FeeMaximum)
        {
            throw Invalid($"{SettingKeys.FeeMinimum} must be 0 or more and not above {SettingKeys.FeeMaximum}.");
        }

        if (settings.TransferMinimum <= 0)
        {
            throw Invalid($"{SettingKeys.TransferMinimum} must be above 0.");
        }

        if (settings.TransferMaximum <= 0)
        {
            throw Invalid($"{SettingKeys.TransferMaximum} must be above 0.");
        }

        if (settings.TransferMinimum > settings.TransferMaximum)
        {
            throw Invalid($"{SettingKeys.TransferMinimum} must not be above {SettingKeys.TransferMaximum}.");
        }

        if (settings.WalletBalanceCap <= 0)
        {
            throw Invalid($"{SettingKeys.WalletBalanceCap} must be above 0.");
        }

        return settings;
    }

    private static InvalidOperationException Invalid(string message) =>
        new($"System settings are inconsistent: {message}");

    private static decimal Require(IReadOnlyDictionary<string, decimal> values, string key) =>
        values.TryGetValue(key, out var value)
            ? value
            : throw new InvalidOperationException($"System setting {key} is missing. Run the DbTool seed.");
}
