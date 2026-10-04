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
    public const string UserViewed = "UserViewed";
    public const string AccountRestricted = "AccountRestricted";
    public const string AccountRestrictionLifted = "AccountRestrictionLifted";

    // Every action above, so a screen can offer them and a filter can refuse any other. A test checks that none is missing.
    public static readonly IReadOnlyList<string> All =
    [
        Register, LoginSucceeded, LoginFailed, AccountLocked, RefreshRotated, RefreshReuseDetected, RefreshGraceUsed, Logout,
        SessionRevoked, TopUp, TopUpFailed, Transfer, TransferFailed, WalletFrozen, WalletUnfrozen, UserViewed, AccountRestricted, AccountRestrictionLifted
    ];
}
