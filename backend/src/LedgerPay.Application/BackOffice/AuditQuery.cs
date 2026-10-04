namespace LedgerPay.Application.BackOffice;

public sealed class AuditQuery
{
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;

    // One of the names in AuditActions.
    public string? Action { get; set; }

    // Part of the name or the email of the person who acted.
    public string? Actor { get; set; }

    public DateOnly? From { get; set; }

    public DateOnly? To { get; set; }
}
