using LedgerPay.Domain.Constants;
using LedgerPay.Domain.Entities;
using LedgerPay.Domain.Enums;

namespace LedgerPay.Domain.Rules;

public static class TransferRules
{
    // Returns the code of the first rule that fails, or null when the transfer may go ahead.
    // The order is fixed so the same request always gets the same answer.
    public static string? FirstFailure(Wallet sender, Wallet? receiver, decimal amount, decimal fee, LedgerSettings settings)
    {
        if (receiver is null)
        {
            return ErrorCodes.RecipientNotFound;
        }

        if (receiver.Id == sender.Id)
        {
            return ErrorCodes.SelfTransferNotAllowed;
        }

        if (amount < settings.TransferMinimum)
        {
            return ErrorCodes.AmountBelowMinimum;
        }

        if (amount > settings.TransferMaximum)
        {
            return ErrorCodes.AmountAboveMaximum;
        }

        if (sender.Status == WalletStatus.Frozen || receiver.Status == WalletStatus.Frozen)
        {
            return ErrorCodes.WalletFrozen;
        }

        if (!AvailableBalance.Covers(sender, amount, fee))
        {
            return ErrorCodes.InsufficientFunds;
        }

        if (receiver.Balance + amount > settings.WalletBalanceCap)
        {
            return ErrorCodes.ReceiverBalanceLimitExceeded;
        }

        return null;
    }
}
