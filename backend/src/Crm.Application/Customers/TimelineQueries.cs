using Crm.Application.Abstractions;
using Crm.Application.Common;
using Crm.Application.Common.Messaging;
using Crm.Application.Common.Paging;
using Crm.Application.Common.Security;
using Crm.Domain.Customers;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Crm.Application.Customers;

public sealed record AddCustomerNoteCommand(Guid CustomerId, string Body) : IRequest<NoteDto>;

public sealed record GetCustomerTimelineQuery(Guid CustomerId, PageRequest Page) : IRequest<PagedResult<TimelineItemDto>>;

internal sealed class AddCustomerNoteValidator : AbstractValidator<AddCustomerNoteCommand>
{
    public AddCustomerNoteValidator() => RuleFor(x => x.Body).NotEmpty().MaximumLength(Note.MaxLength);
}

internal sealed class TimelineHandlers(IAppDbContext db, ICurrentUser currentUser, IUserDirectory users) :
    IRequestHandler<AddCustomerNoteCommand, NoteDto>,
    IRequestHandler<GetCustomerTimelineQuery, PagedResult<TimelineItemDto>>
{
    private const int ExcerptLength = 160;

    public async Task<NoteDto> Handle(AddCustomerNoteCommand request, CancellationToken ct)
    {
        await EnsureCustomerExists(request.CustomerId, ct);
        var authorId = currentUser.RequiredUserId;
        var note = new Note(request.CustomerId, request.Body, authorId);
        db.Notes.Add(note);
        await db.SaveChangesAsync(ct);

        var author = await users.GetAsync(authorId, ct);
        return new NoteDto(note.Id, note.Body, new UserRefDto(authorId, author?.FullName ?? string.Empty), note.CreatedAt);
    }

    /// <summary>
    /// Merges tickets, ticket messages and notes for the customer, newest first. Each source is read up to the
    /// end of the requested page so paging stays correct without loading the full history.
    /// </summary>
    public async Task<PagedResult<TimelineItemDto>> Handle(GetCustomerTimelineQuery request, CancellationToken ct)
    {
        await EnsureCustomerExists(request.CustomerId, ct);
        var take = request.Page.Skip + request.Page.SafePageSize;
        var customerId = request.CustomerId;
        var tickets = db.Tickets.AsNoTracking().Where(t => t.CustomerId == customerId).VisibleTo(currentUser);

        var ticketItems = await tickets
            .OrderByDescending(t => t.CreatedAt).Take(take)
            .Select(t => new { t.Id, t.CreatedAt, t.ReferenceNumber, t.Subject, t.Description, t.Channel, t.CreatedBy })
            .ToListAsync(ct);

        var messageItems = await db.Messages.AsNoTracking()
            .Where(m => tickets.Select(t => t.Id).Contains(m.TicketId))
            .OrderByDescending(m => m.CreatedAt).Take(take)
            .Select(m => new { m.TicketId, m.CreatedAt, m.Body, m.Channel, m.IsInternal, m.Direction, m.SenderUserId })
            .ToListAsync(ct);

        var noteItems = await db.Notes.AsNoTracking()
            .Where(n => n.CustomerId == customerId)
            .OrderByDescending(n => n.CreatedAt).Take(take)
            .Select(n => new { n.CreatedAt, n.Body, n.AuthorId })
            .ToListAsync(ct);

        var totals = await tickets.CountAsync(ct)
            + await db.Messages.CountAsync(m => tickets.Select(t => t.Id).Contains(m.TicketId), ct)
            + await db.Notes.CountAsync(n => n.CustomerId == customerId, ct);

        var userIds = ticketItems.Select(t => t.CreatedBy)
            .Concat(messageItems.Select(m => m.SenderUserId))
            .Concat(noteItems.Select(n => (Guid?)n.AuthorId))
            .OfType<Guid>();
        var names = await users.GetManyAsync(userIds, ct);
        UserRefDto? Ref(Guid? id) => id is { } u ? new UserRefDto(u, names.TryGetValue(u, out var s) ? s.FullName : string.Empty) : null;

        var items = ticketItems
            .Select(t => new TimelineItemDto(TimelineKind.Ticket, t.CreatedAt, $"{t.ReferenceNumber} {t.Subject}", Excerpt(t.Description), t.Channel, t.Id, Ref(t.CreatedBy)))
            .Concat(messageItems.Select(m => new TimelineItemDto(
                TimelineKind.Message, m.CreatedAt,
                m.IsInternal ? "InternalNote" : m.Direction == Domain.Tickets.MessageDirection.Inbound ? "CustomerMessage" : "Reply",
                Excerpt(m.Body), m.Channel, m.TicketId, Ref(m.SenderUserId))))
            .Concat(noteItems.Select(n => new TimelineItemDto(TimelineKind.Note, n.CreatedAt, "Note", Excerpt(n.Body), null, null, Ref(n.AuthorId))))
            .OrderByDescending(i => i.OccurredAt)
            .Skip(request.Page.Skip)
            .Take(request.Page.SafePageSize)
            .ToList();

        return new PagedResult<TimelineItemDto>(items, request.Page.SafePage, request.Page.SafePageSize, totals);
    }

    private async Task EnsureCustomerExists(Guid customerId, CancellationToken ct)
    {
        if (!await db.Customers.AnyAsync(c => c.Id == customerId, ct))
        {
            throw new NotFoundException("Customer", customerId);
        }
    }

    private static string Excerpt(string text) => text.Length <= ExcerptLength ? text : string.Concat(text.AsSpan(0, ExcerptLength), "…");
}
