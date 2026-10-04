namespace LedgerPay.Domain.Entities;

public sealed class IdempotencyKey
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public int? ResponseStatusCode { get; set; }
    public string? ResponseBody { get; set; }
    public Guid? TransactionId { get; set; }
    public DateTime CreatedAt { get; set; }
}
