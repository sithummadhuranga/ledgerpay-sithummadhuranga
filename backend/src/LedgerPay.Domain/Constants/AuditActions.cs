namespace LedgerPay.Domain.Constants;

public static class AuditActions
{
    public const string Register = "Register";
    public const string LoginSucceeded = "LoginSucceeded";
    public const string LoginFailed = "LoginFailed";
    public const string AccountLocked = "AccountLocked";
    public const string RefreshRotated = "RefreshRotated";
    public const string RefreshReuseDetected = "RefreshReuseDetected";
    public const string RefreshGraceUsed = "RefreshGraceUsed";
    public const string Logout = "Logout";
    public const string SessionRevoked = "SessionRevoked";
    public const string TopUp = "TopUp";
    public const string TopUpFailed = "TopUpFailed";
    public const string Transfer = "Transfer";
    public const string TransferFailed = "TransferFailed";
    public const string WalletFrozen = "WalletFrozen";
    public const string WalletUnfrozen = "WalletUnfrozen";
}
