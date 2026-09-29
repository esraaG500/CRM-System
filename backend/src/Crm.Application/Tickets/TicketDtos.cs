using Crm.Application.Customers;
using Crm.Domain.Common;
using Crm.Domain.Tickets;

namespace Crm.Application.Tickets;

public sealed record NamedRefDto(Guid Id, string NameEn, string NameAr);

public sealed record CustomerRefDto(Guid Id, string Name, string ReferenceNumber);

public sealed record TicketSummaryDto(
    Guid Id,
    string ReferenceNumber,
    string Subject,
    TicketStatus Status,
    TicketPriority Priority,
    Channel Channel,
    CustomerRefDto Customer,
    UserRefDto? Assignee,
    NamedRefDto? Category,
    NamedRefDto? Department,
    int EscalationLevel,
    DateTimeOffset? FirstRespondedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record TicketDto(
    Guid Id,
    string ReferenceNumber,
    string Subject,
    string Description,
    TicketStatus Status,
    TicketPriority Priority,
    Channel Channel,
    CustomerRefDto Customer,
    ContactPersonDto? ContactPerson,
    UserRefDto? Assignee,
    NamedRefDto? Category,
    NamedRefDto? Department,
    NamedRefDto? Branch,
    int EscalationLevel,
    DateTimeOffset? FirstRespondedAt,
    DateTimeOffset? ResolvedAt,
    DateTimeOffset? ClosedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    IReadOnlyList<TicketStatus> AllowedTransitions,
    string Version);

public sealed record TicketHistoryEntryDto(
    DateTimeOffset Timestamp,
    UserRefDto? User,
    TicketChangeType ChangeType,
    string? Field,
    string? OldValue,
    string? NewValue,
    string? Reason);

public enum SenderKind { Agent, Customer, System }

public sealed record MessageSenderDto(SenderKind Kind, Guid? Id, string Name);

public sealed record MessageDto(
    Guid Id,
    Guid TicketId,
    MessageDirection Direction,
    bool IsInternal,
    Channel Channel,
    string Body,
    MessageSenderDto Sender,
    IReadOnlyList<UserRefDto> Mentions,
    DeliveryStatus? DeliveryStatus,
    string? DeliveryError,
    DateTimeOffset CreatedAt);
