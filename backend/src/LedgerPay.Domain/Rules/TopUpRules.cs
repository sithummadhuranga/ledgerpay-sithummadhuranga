using LedgerPay.Domain.Constants;
using LedgerPay.Domain.Entities;
using LedgerPay.Domain.Enums;

namespace LedgerPay.Domain.Rules;

public static class TopUpRules
{
    // Returns the code of the first rule that fails, or null when the top-up may go ahead.
    // bankReferenceUsed means a completed top-up already carries this bank reference.
    public static string? FirstFailure(Wallet? wallet, bool bankReferenceUsed, decimal amount, LedgerSettings settings)
    {
        if (wallet is null)
        {
            return ErrorCodes.WalletNotFound;
        }

        if (wallet.Status == WalletStatus.Frozen)
        {
            return ErrorCodes.WalletFrozen;
        }

        if (bankReferenceUsed)
        {
            return ErrorCodes.DuplicateBankReference;
        }

        if (wallet.Balance + amount > settings.WalletBalanceCap)
        {
            return ErrorCodes.BalanceLimitExceeded;
        }

        return null;
    }
}
