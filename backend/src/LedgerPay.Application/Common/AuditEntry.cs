using LedgerPay.Domain.Entities;

namespace LedgerPay.Application.Common;

public static class AuditEntry
{
    public static AuditLog Create(
        string action,
        string entityType,
        string? entityReference,
        string? details,
        Guid actorUserId,
        RequestInfo info,
        DateTime now)
    {
        return new AuditLog
        {
            CreatedAt = now,
            ActorUserId = actorUserId,
            Action = action,
            EntityType = entityType,
            EntityReference = entityReference,
            IpAddress = info.IpAddress,
            CorrelationId = info.CorrelationId,
            Details = details
        };
    }
}
