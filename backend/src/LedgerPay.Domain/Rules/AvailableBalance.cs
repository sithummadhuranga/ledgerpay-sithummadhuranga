using LedgerPay.Domain.Entities;

namespace LedgerPay.Domain.Rules;

public static class AvailableBalance
{
    // There are no holds at this level, so the available balance is the balance. Holds would be subtracted here.
    public static decimal Of(Wallet wallet) => wallet.Balance;

    public static bool Covers(Wallet wallet, decimal amount, decimal fee) => Of(wallet) >= amount + fee;
}
