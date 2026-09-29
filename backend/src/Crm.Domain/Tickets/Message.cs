using Crm.Domain.Common;

namespace Crm.Domain.Tickets;

public enum MessageDirection { Inbound, Outbound }

public enum DeliveryStatus { Pending, Sent, Delivered, Read, Failed }

/// <summary>One communication on a ticket. Internal notes are never visible to customers.</summary>
public sealed class Message : Entity
{
    public const int MaxBodyLength = 20000;

    private readonly List<MessageMention> _mentions = [];

    private Message() { }

    private Message(Guid ticketId, MessageDirection direction, bool isInternal, Channel channel, string body,
        Guid? senderUserId, Guid? senderCustomerId, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            throw new DomainException("message.body-required", "Message text is required.");
        }

        if (body.Length > MaxBodyLength)
        {
            throw new DomainException("message.too-long", $"Message must be {MaxBodyLength} characters or fewer.");
        }

        TicketId = ticketId;
        Direction = direction;
        IsInternal = isInternal;
        Channel = channel;
        Body = body.Trim();
        SenderUserId = senderUserId;
        SenderCustomerId = senderCustomerId;
        CreatedAt = now;
    }

    public Guid TicketId { get; private set; }
    public MessageDirection Direction { get; private set; }
    public bool IsInternal { get; private set; }
    public Channel Channel { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public Guid? SenderUserId { get; private set; }
    public Guid? SenderCustomerId { get; private set; }
    public DeliveryStatus? DeliveryStatus { get; private set; }
    public string? DeliveryError { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyCollection<MessageMention> Mentions => _mentions.AsReadOnly();

    public static Message AgentReply(Guid ticketId, Channel channel, string body, Guid agentId, DateTimeOffset now) =>
        new(ticketId, MessageDirection.Outbound, isInternal: false, channel, body, agentId, null, now)
        {
            DeliveryStatus = Tickets.DeliveryStatus.Pending,
        };

    public static Message InternalNote(Guid ticketId, string body, Guid agentId, IEnumerable<Guid> mentionUserIds, DateTimeOffset now)
    {
        var message = new Message(ticketId, MessageDirection.Outbound, isInternal: true, Channel.Api, body, agentId, null, now);
        foreach (var userId in mentionUserIds.Distinct())
        {
            message._mentions.Add(new MessageMention(message.Id, userId));
        }

        return message;
    }

    public static Message CustomerMessage(Guid ticketId, Channel channel, string body, Guid customerId, DateTimeOffset now) =>
        new(ticketId, MessageDirection.Inbound, isInternal: false, channel, body, null, customerId, now);

    public void MarkDelivery(DeliveryStatus status, string? error = null)
    {
        DeliveryStatus = status;
        DeliveryError = error;
    }
}

public sealed record MessageMention(Guid MessageId, Guid UserId);
