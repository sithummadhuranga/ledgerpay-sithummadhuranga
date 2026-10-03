using LedgerPay.Domain.Constants;

namespace LedgerPay.Application.Common;

// What each error code means over HTTP. The idempotency service stores the status with its answer,
// and the API will build Problem Details from the same entries.
public static class ErrorCatalog
{
    private static readonly Dictionary<string, ErrorInfo> Entries = new()
    {
        [ErrorCodes.ValidationFailed] = new(400, "One or more fields are not valid"),
        [ErrorCodes.IdempotencyKeyRequired] = new(400, "The Idempotency-Key header is required"),
        [ErrorCodes.Unauthenticated] = new(401, "Sign in to continue"),
        [ErrorCodes.InvalidCredentials] = new(401, "Email or password is wrong"),
        [ErrorCodes.Forbidden] = new(403, "You do not have access to this"),
        [ErrorCodes.AccountLocked] = new(423, "Account is locked after too many failed sign-ins"),
        [ErrorCodes.WalletNotFound] = new(404, "Wallet not found"),
        [ErrorCodes.TransactionNotFound] = new(404, "Transaction not found"),
        [ErrorCodes.RecipientNotFound] = new(404, "Recipient not found"),
        [ErrorCodes.EmailAlreadyRegistered] = new(409, "Email is already registered"),
        [ErrorCodes.PhoneAlreadyRegistered] = new(409, "Mobile number is already registered"),
        [ErrorCodes.DuplicateBankReference] = new(409, "Bank reference was already used by a completed top-up"),
        [ErrorCodes.IdempotencyKeyReused] = new(409, "The Idempotency-Key was already used for a different request"),
        [ErrorCodes.WalletAlreadyInState] = new(409, "Wallet is already in that state"),
        [ErrorCodes.SelfTransferNotAllowed] = new(422, "You cannot send money to your own wallet"),
        [ErrorCodes.AmountBelowMinimum] = new(422, "Amount is below the minimum transfer"),
        [ErrorCodes.AmountAboveMaximum] = new(422, "Amount is above the maximum transfer"),
        [ErrorCodes.WalletFrozen] = new(422, "A frozen wallet cannot send or receive"),
        [ErrorCodes.InsufficientFunds] = new(422, "Balance does not cover the amount and fee"),
        [ErrorCodes.ReceiverBalanceLimitExceeded] = new(422, "The recipient's balance would pass the wallet limit"),
        [ErrorCodes.BalanceLimitExceeded] = new(422, "Balance would pass the wallet limit"),
        [ErrorCodes.RateLimited] = new(429, "Too many requests"),
        [ErrorCodes.InternalError] = new(500, "Something went wrong on our side")
    };

    public static ErrorInfo Describe(string code) =>
        Entries.TryGetValue(code, out var info)
            ? info
            : throw new ArgumentException($"Error code {code} has no entry in the error catalog.", nameof(code));
}
