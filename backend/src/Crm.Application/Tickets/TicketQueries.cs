using Crm.Application.Abstractions;
using Crm.Application.Common.Messaging;
using Crm.Application.Common.Paging;
using Crm.Application.Common.Security;
using Crm.Domain.Common;
using Crm.Domain.Tickets;
using Microsoft.EntityFrameworkCore;

namespace Crm.Application.Tickets;

public enum TicketSort { CreatedAtDesc, CreatedAt, Priority, UpdatedAtDesc }

public sealed record ListTicketsQuery(
    string? Q = null,
    IReadOnlyList<TicketStatus>? Status = null,
    IReadOnlyList<TicketPriority>? Priority = null,
    Guid? CategoryId = null,
    Guid? DepartmentId = null,
    Guid? BranchId = null,
    string? AssigneeId = null,
    Guid? CustomerId = null,
    Channel? Channel = null,
    DateOnly? CreatedFrom = null,
    DateOnly? CreatedTo = null,
    TicketSort Sort = TicketSort.CreatedAtDesc,
    PageRequest? Page = null) : IRequest<PagedResult<TicketSummaryDto>>;

public sealed record GetTicketQuery(Guid TicketId) : IRequest<TicketDto>;

public sealed record GetTicketHistoryQuery(Guid TicketId) : IRequest<IReadOnlyList<TicketHistoryEntryDto>>;

internal sealed class TicketQueryHandlers(IAppDbContext db, ICurrentUser currentUser, IUserDirectory users, TicketReader reader) :
    IRequestHandler<ListTicketsQuery, PagedResult<TicketSummaryDto>>,
    IRequestHandler<GetTicketQuery, TicketDto>,
    IRequestHandler<GetTicketHistoryQuery, IReadOnlyList<TicketHistoryEntryDto>>
{
    public async Task<PagedResult<TicketSummaryDto>> Handle(ListTicketsQuery r, CancellationToken ct)
    {
        var page = r.Page ?? new PageRequest();
        var query = db.Tickets.AsNoTracking().VisibleTo(currentUser);

        if (!string.IsNullOrWhiteSpace(r.Q))
        {
            var term = r.Q.Trim();
            var reference = term.ToUpperInvariant();
            var like = $"%{term.Replace("[", "[[]", StringComparison.Ordinal).Replace("%", "[%]", StringComparison.Ordinal)}%";
            query = query.Where(t => t.ReferenceNumber == reference || EF.Functions.Like(t.Subject, like)
                || db.Customers.Any(c => c.Id == t.CustomerId && EF.Functions.Like(c.Name, like)));
        }

        if (r.Status is { Count: > 0 })
        {
            query = query.Where(t => r.Status.Contains(t.Status));
        }

        if (r.Priority is { Count: > 0 })
        {
            query = query.Where(t => r.Priority.Contains(t.Priority));
        }

        if (r.CategoryId is not null) query = query.Where(t => t.CategoryId == r.CategoryId);
        if (r.DepartmentId is not null) query = query.Where(t => t.DepartmentId == r.DepartmentId);
        if (r.BranchId is not null) query = query.Where(t => t.BranchId == r.BranchId);
        if (r.CustomerId is not null) query = query.Where(t => t.CustomerId == r.CustomerId);
        if (r.Channel is not null) query = query.Where(t => t.Channel == r.Channel);
        if (r.CreatedFrom is { } from) query = query.Where(t => t.CreatedAt >= new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
        if (r.CreatedTo is { } to) query = query.Where(t => t.CreatedAt < new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));

        switch (r.AssigneeId)
        {
            case null or "":
                break;
            case "me":
                var me = currentUser.RequiredUserId;
                query = query.Where(t => t.AssigneeId == me);
                break;
            case "unassigned":
                query = query.Where(t => t.AssigneeId == null);
                break;
            default:
                var assignee = Guid.TryParse(r.AssigneeId, out var id) ? id : Guid.Empty;
                query = query.Where(t => t.AssigneeId == assignee);
                break;
        }

        query = r.Sort switch
        {
            TicketSort.CreatedAt => query.OrderBy(t => t.CreatedAt),
            TicketSort.Priority => query.OrderByDescending(t => t.Priority).ThenBy(t => t.CreatedAt),
            TicketSort.UpdatedAtDesc => query.OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt),
            _ => query.OrderByDescending(t => t.CreatedAt),
        };

        var total = await query.CountAsync(ct);
        var items = await reader.ToSummaries(query.Skip(page.Skip).Take(page.SafePageSize), ct);
        return new PagedResult<TicketSummaryDto>(items, page.SafePage, page.SafePageSize, total);
    }

    public async Task<TicketDto> Handle(GetTicketQuery request, CancellationToken ct)
    {
        var ticket = await reader.LoadForUpdate(request.TicketId, ct);
        return await reader.ToDto(ticket, ct);
    }

    public async Task<IReadOnlyList<TicketHistoryEntryDto>> Handle(GetTicketHistoryQuery request, CancellationToken ct)
    {
        await reader.LoadForUpdate(request.TicketId, ct);
        var entries = await db.TicketHistory.AsNoTracking()
            .Where(h => h.TicketId == request.TicketId)
            .OrderBy(h => h.Timestamp).ThenBy(h => h.Id)
            .ToListAsync(ct);
        var names = await users.GetManyAsync(entries.Select(e => e.UserId).OfType<Guid>(), ct);

        return entries.Select(e => new TicketHistoryEntryDto(
                e.Timestamp,
                e.UserId is { } u ? new Customers.UserRefDto(u, names.TryGetValue(u, out var s) ? s.FullName : string.Empty) : null,
                e.ChangeType, e.Field, e.OldValue, e.NewValue, e.Reason))
            .ToList();
    }
}
