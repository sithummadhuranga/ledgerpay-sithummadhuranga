namespace LedgerPay.Application.Wallets;

// All a stranger may learn about a wallet: its number, a masked holder name and whether it can receive money.
public sealed record LookupResponse(string WalletNumber, string HolderName, bool Active);
