namespace LedgerPay.Application.BackOffice;

public sealed class UserSearchQuery
{
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;

    // Part of a name, an email, a mobile number or a wallet number.
    public string? Search { get; set; }

    public UserStatusFilter? Status { get; set; }
}
