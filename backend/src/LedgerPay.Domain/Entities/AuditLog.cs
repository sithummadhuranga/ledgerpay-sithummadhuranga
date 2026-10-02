namespace LedgerPay.Domain.Entities;

public sealed class AuditLog
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? ActorUserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityReference { get; set; }
    public string? IpAddress { get; set; }
    public string? CorrelationId { get; set; }
    public string? Details { get; set; }
}
