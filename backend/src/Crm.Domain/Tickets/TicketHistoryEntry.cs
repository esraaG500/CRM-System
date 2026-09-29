namespace Crm.Domain.Tickets;

public enum TicketChangeType { Created, FieldChanged, Assigned, StatusChanged, Escalated, SlaBreached, Merged, Reopened }

/// <summary>Append-only record of one change on a ticket.</summary>
public sealed record TicketHistoryEntry(
    Guid TicketId,
    DateTimeOffset Timestamp,
    Guid? UserId,
    TicketChangeType ChangeType,
    string? Field,
    string? OldValue,
    string? NewValue,
    string? Reason)
{
    public long Id { get; private init; }
}
