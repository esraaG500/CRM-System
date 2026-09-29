using Crm.Application.Abstractions;
using Crm.Application.Common.Messaging;
using Crm.Application.Customers;
using Crm.Domain.Common;
using Crm.Domain.Tickets;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Crm.Application.Tickets;

public sealed record ListMessagesQuery(Guid TicketId, bool IncludeInternal = true) : IRequest<IReadOnlyList<MessageDto>>;

public sealed record ReplyToTicketCommand(Guid TicketId, string Body, TicketStatus? SetStatus) : IRequest<MessageDto>;

public sealed record AddInternalNoteCommand(Guid TicketId, string Body, IReadOnlyList<Guid>? MentionUserIds) : IRequest<MessageDto>;

internal sealed class ReplyToTicketValidator : AbstractValidator<ReplyToTicketCommand>
{
    public ReplyToTicketValidator()
    {
        RuleFor(x => x.Body).NotEmpty().MaximumLength(Message.MaxBodyLength);
        RuleFor(x => x.SetStatus).IsInEnum().When(x => x.SetStatus is not null);
    }
}

internal sealed class AddInternalNoteValidator : AbstractValidator<AddInternalNoteCommand>
{
    public AddInternalNoteValidator()
    {
        RuleFor(x => x.Body).NotEmpty().MaximumLength(Message.MaxBodyLength);
        RuleFor(x => x.MentionUserIds).Must(m => m is null || m.Count <= 20).WithMessage("You can mention at most 20 people.");
    }
}

internal sealed class TicketConversationHandlers(
    IAppDbContext db, ICurrentUser currentUser, IUserDirectory users, IClock clock, TicketReader reader) :
    IRequestHandler<ListMessagesQuery, IReadOnlyList<MessageDto>>,
    IRequestHandler<ReplyToTicketCommand, MessageDto>,
    IRequestHandler<AddInternalNoteCommand, MessageDto>
{
    public async Task<IReadOnlyList<MessageDto>> Handle(ListMessagesQuery request, CancellationToken ct)
    {
        var ticket = await reader.LoadForUpdate(request.TicketId, ct);
        var messages = await db.Messages.AsNoTracking()
            .Include(m => m.Mentions)
            .Where(m => m.TicketId == request.TicketId && (request.IncludeInternal || !m.IsInternal))
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(ct);

        var customerName = await db.Customers.IgnoreQueryFilters().Where(c => c.Id == ticket.CustomerId).Select(c => c.Name).SingleAsync(ct);
        var names = await users.GetManyAsync(
            messages.Select(m => m.SenderUserId).OfType<Guid>().Concat(messages.SelectMany(m => m.Mentions.Select(x => x.UserId))), ct);

        return messages.Select(m => ToDto(m, names, customerName)).ToList();
    }

    public async Task<MessageDto> Handle(ReplyToTicketCommand r, CancellationToken ct)
    {
        var ticket = await reader.LoadForUpdate(r.TicketId, ct);
        var agentId = currentUser.RequiredUserId;
        var now = clock.UtcNow;

        ticket.RecordAgentReply(agentId, now);
        var message = Message.AgentReply(ticket.Id, ticket.Channel, r.Body, agentId, now);

        // Outbound channel delivery (email, WhatsApp, SMS) is added with the channel adapters (US6).
        // Until then replies are recorded against the ticket and visible in the customer portal.
        message.MarkDelivery(DeliveryStatus.Sent);

        if (r.SetStatus is { } status)
        {
            ticket.ChangeStatus(status, agentId, now);
        }

        db.Messages.Add(message);
        AddNewHistory(ticket);
        await db.SaveChangesAsync(ct);

        return await Single(message, ct);
    }

    public async Task<MessageDto> Handle(AddInternalNoteCommand r, CancellationToken ct)
    {
        var ticket = await reader.LoadForUpdate(r.TicketId, ct);
        var mentions = r.MentionUserIds ?? [];
        if (mentions.Count > 0)
        {
            var found = await users.GetManyAsync(mentions, ct);
            var unknown = mentions.Where(id => !found.ContainsKey(id)).ToList();
            if (unknown.Count > 0)
            {
                throw new ValidationException([new FluentValidation.Results.ValidationFailure("mentionUserIds", "One or more mentioned users do not exist.")]);
            }
        }

        var note = Message.InternalNote(ticket.Id, r.Body, currentUser.RequiredUserId, mentions, clock.UtcNow);
        db.Messages.Add(note);
        await db.SaveChangesAsync(ct);

        return await Single(note, ct);
    }

    private void AddNewHistory(Ticket ticket)
    {
        foreach (var entry in ticket.History.Where(h => h.Id == 0))
        {
            db.TicketHistory.Add(entry);
        }
    }

    private async Task<MessageDto> Single(Message message, CancellationToken ct)
    {
        var names = await users.GetManyAsync(message.Mentions.Select(m => m.UserId).Append(message.SenderUserId ?? Guid.Empty), ct);
        return ToDto(message, names, customerName: string.Empty);
    }

    private static MessageDto ToDto(Message m, IReadOnlyDictionary<Guid, StaffUser> names, string customerName)
    {
        MessageSenderDto sender = m switch
        {
            { SenderUserId: { } u } => new(SenderKind.Agent, u, names.TryGetValue(u, out var s) ? s.FullName : string.Empty),
            { SenderCustomerId: { } c } => new(SenderKind.Customer, c, customerName),
            _ => new(SenderKind.System, null, "System"),
        };

        return new MessageDto(
            m.Id, m.TicketId, m.Direction, m.IsInternal, m.Channel, m.Body, sender,
            m.Mentions.Select(x => new UserRefDto(x.UserId, names.TryGetValue(x.UserId, out var n) ? n.FullName : string.Empty)).ToList(),
            m.DeliveryStatus, m.DeliveryError, m.CreatedAt);
    }
}
