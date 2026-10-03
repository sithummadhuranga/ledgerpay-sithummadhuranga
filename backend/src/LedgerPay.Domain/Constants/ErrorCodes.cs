namespace LedgerPay.Domain.Constants;

public static class ErrorCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string IdempotencyKeyRequired = "IDEMPOTENCY_KEY_REQUIRED";
    public const string Unauthenticated = "UNAUTHENTICATED";
    public const string InvalidCredentials = "INVALID_CREDENTIALS";
    public const string Forbidden = "FORBIDDEN";
    public const string AccountLocked = "ACCOUNT_LOCKED";
    public const string WalletNotFound = "WALLET_NOT_FOUND";
    public const string TransactionNotFound = "TRANSACTION_NOT_FOUND";
    public const string RecipientNotFound = "RECIPIENT_NOT_FOUND";
    public const string EmailAlreadyRegistered = "EMAIL_ALREADY_REGISTERED";
    public const string PhoneAlreadyRegistered = "PHONE_ALREADY_REGISTERED";
    public const string DuplicateBankReference = "DUPLICATE_BANK_REFERENCE";
    public const string IdempotencyKeyReused = "IDEMPOTENCY_KEY_REUSED";
    public const string WalletAlreadyInState = "WALLET_ALREADY_IN_STATE";
    public const string SelfTransferNotAllowed = "SELF_TRANSFER_NOT_ALLOWED";
    public const string AmountBelowMinimum = "AMOUNT_BELOW_MINIMUM";
    public const string AmountAboveMaximum = "AMOUNT_ABOVE_MAXIMUM";
    public const string WalletFrozen = "WALLET_FROZEN";
    public const string InsufficientFunds = "INSUFFICIENT_FUNDS";
    public const string ReceiverBalanceLimitExceeded = "RECEIVER_BALANCE_LIMIT_EXCEEDED";
    public const string BalanceLimitExceeded = "BALANCE_LIMIT_EXCEEDED";
    public const string RateLimited = "RATE_LIMITED";
    public const string InternalError = "INTERNAL_ERROR";
}
