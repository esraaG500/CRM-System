using Crm.Application.Abstractions;
using Crm.Application.Common;
using Crm.Application.Common.Security;
using Crm.Application.Customers;
using Crm.Domain.Tickets;
using Microsoft.EntityFrameworkCore;

namespace Crm.Application.Tickets;

/// <summary>Shared loading and projection of tickets for the ticket use cases.</summary>
internal sealed class TicketReader(IAppDbContext db, ICurrentUser currentUser, IUserDirectory users)
{
    /// <summary>Loads a tracked ticket the current user may see, or throws <see cref="NotFoundException"/>.</summary>
    public async Task<Ticket> LoadForUpdate(Guid ticketId, CancellationToken ct)
    {
        var ticket = await db.Tickets.FirstOrDefaultAsync(t => t.Id == ticketId, ct);
        if (ticket is null || !currentUser.CanSee(ticket))
        {
            throw new NotFoundException("Ticket", ticketId);
        }

        return ticket;
    }

    public async Task<TicketDto> ToDto(Ticket t, CancellationToken ct)
    {
        var customer = await db.Customers.AsNoTracking().IgnoreQueryFilters()
            .Where(c => c.Id == t.CustomerId)
            .Select(c => new CustomerRefDto(c.Id, c.Name, c.ReferenceNumber))
            .SingleAsync(ct);
        var contact = t.ContactPersonId is null
            ? null
            : await db.ContactPeople.AsNoTracking().Where(p => p.Id == t.ContactPersonId).Select(p => p.ToDto()).FirstOrDefaultAsync(ct);
        var category = await db.Categories.AsNoTracking().Where(c => c.Id == t.CategoryId)
            .Select(c => new NamedRefDto(c.Id, c.NameEn, c.NameAr)).FirstOrDefaultAsync(ct);
        var department = await db.Departments.AsNoTracking().Where(d => d.Id == t.DepartmentId)
            .Select(d => new NamedRefDto(d.Id, d.NameEn, d.NameAr)).FirstOrDefaultAsync(ct);
        var branch = t.BranchId is null
            ? null
            : await db.Branches.AsNoTracking().Where(b => b.Id == t.BranchId)
                .Select(b => new NamedRefDto(b.Id, b.NameEn, b.NameAr)).FirstOrDefaultAsync(ct);
        var assignee = t.AssigneeId is { } a ? await users.GetAsync(a, ct) : null;

        return new TicketDto(
            t.Id, t.ReferenceNumber, t.Subject, t.Description, t.Status, t.Priority, t.Channel,
            customer, contact,
            assignee is null ? null : new UserRefDto(assignee.Id, assignee.FullName),
            category, department, branch,
            t.EscalationLevel, t.FirstRespondedAt, t.ResolvedAt, t.ClosedAt, t.CreatedAt, t.UpdatedAt,
            t.AllowedTransitions(), t.RowVersion.ToVersion());
    }

    /// <summary>Projects a ticket query to summaries, resolving assignee names in one directory lookup.</summary>
    public async Task<List<TicketSummaryDto>> ToSummaries(IQueryable<Ticket> query, CancellationToken ct)
    {
        var rows = await query
            .Select(t => new
            {
                t.Id, t.ReferenceNumber, t.Subject, t.Status, t.Priority, t.Channel, t.AssigneeId, t.EscalationLevel,
                t.FirstRespondedAt, t.CreatedAt, t.UpdatedAt,
                Customer = db.Customers.IgnoreQueryFilters().Where(c => c.Id == t.CustomerId)
                    .Select(c => new CustomerRefDto(c.Id, c.Name, c.ReferenceNumber)).First(),
                Category = db.Categories.Where(c => c.Id == t.CategoryId).Select(c => new NamedRefDto(c.Id, c.NameEn, c.NameAr)).FirstOrDefault(),
                Department = db.Departments.Where(d => d.Id == t.DepartmentId).Select(d => new NamedRefDto(d.Id, d.NameEn, d.NameAr)).FirstOrDefault(),
            })
            .ToListAsync(ct);

        var names = await users.GetManyAsync(rows.Select(r => r.AssigneeId).OfType<Guid>(), ct);
        return rows.Select(r => new TicketSummaryDto(
                r.Id, r.ReferenceNumber, r.Subject, r.Status, r.Priority, r.Channel, r.Customer,
                r.AssigneeId is { } id && names.TryGetValue(id, out var u) ? new UserRefDto(u.Id, u.FullName) : null,
                r.Category, r.Department, r.EscalationLevel, r.FirstRespondedAt, r.CreatedAt, r.UpdatedAt))
            .ToList();
    }
}
