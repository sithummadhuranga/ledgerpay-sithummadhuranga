namespace LedgerPay.Application.Admin;

// Restricted is nullable so a body without it can be told apart from false, which means lift the restriction.
public sealed class StaffRestrictionRequest
{
    public string Email { get; set; } = string.Empty;

    public bool? Restricted { get; set; }

    public string? Reason { get; set; }
}
