namespace LedgerPay.Domain.Constants;

public static class SettingKeys
{
    // The fee percent is stored as a percent, so 0.50 means 0.5 percent.
    public const string FeePercent = "Fee.Percent";
    public const string FeeMinimum = "Fee.Minimum";
    public const string FeeMaximum = "Fee.Maximum";
    public const string TransferMinimum = "Transfer.Minimum";
    public const string TransferMaximum = "Transfer.Maximum";
    public const string WalletBalanceCap = "Wallet.BalanceCap";
}
