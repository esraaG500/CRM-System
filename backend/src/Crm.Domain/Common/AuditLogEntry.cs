namespace Crm.Domain.Common;

/// <summary>Append-only record of a change or security event.</summary>
public sealed class AuditLogEntry
{
    public long Id { get; private set; }
    public DateTimeOffset Timestamp { get; init; }
    public Guid? UserId { get; init; }
    public required string Action { get; init; }
    public required string EntityType { get; init; }
    public string? EntityId { get; init; }
    public string? Changes { get; init; }
    public string? IpAddress { get; init; }
    public string? CorrelationId { get; init; }
}
