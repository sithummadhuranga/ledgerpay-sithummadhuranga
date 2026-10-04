namespace LedgerPay.Domain.Rules;

public static class LoginLockout
{
    public const int MaxFailedAttempts = 5;

    public static readonly TimeSpan Duration = TimeSpan.FromMinutes(15);
}
