namespace LedgerPay.Application.BackOffice;

// Active is a wallet that is not frozen and an account that is not locked. The other two are what needs a look.
public enum UserStatusFilter
{
    Active,
    Frozen,
    Locked
}
