using Crm.Application.Abstractions;
using Crm.Application.Common;
using Crm.Application.Common.Messaging;
using Crm.Application.Common.Security;
using Crm.Domain.Common;
using Crm.Domain.Tickets;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Crm.Application.Tickets;

public sealed record CreateTicketCommand(
    string Subject,
    string Description,
    Guid CustomerId,
    Guid? ContactPersonId,
    Channel Channel,
    Guid CategoryId,
    TicketPriority Priority,
    Guid? DepartmentId,
    Guid? AssigneeId) : IRequest<TicketDto>;

public sealed record UpdateTicketCommand(
    Guid TicketId,
    string? Subject,
    Guid? CategoryId,
    TicketPriority? Priority,
    Guid? DepartmentId,
    string Version) : IRequest<TicketDto>;

public sealed record AssignTicketCommand(Guid TicketId, Guid? AssigneeId, Guid? DepartmentId, string Version) : IRequest<TicketDto>;

public sealed record TakeTicketCommand(Guid TicketId) : IRequest<TicketDto>;

public sealed record ChangeTicketStatusCommand(Guid TicketId, TicketStatus Status, string? Reason, string Version) : IRequest<TicketDto>;

public sealed record EscalateTicketCommand(Guid TicketId, string Reason, string Version) : IRequest<TicketDto>;

internal sealed class CreateTicketValidator : AbstractValidator<CreateTicketCommand>
{
    public CreateTicketValidator()
    {
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(Ticket.SubjectMaxLength);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(Message.MaxBodyLength);
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Channel).IsInEnum();
        RuleFor(x => x.Priority).IsInEnum();
    }
}

internal sealed class UpdateTicketValidator : AbstractValidator<UpdateTicketCommand>
{
    public UpdateTicketValidator()
    {
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(Ticket.SubjectMaxLength).When(x => x.Subject is not null);
        RuleFor(x => x.Priority).IsInEnum().When(x => x.Priority is not null);
        RuleFor(x => x.Version).NotEmpty();
    }
}

internal sealed class AssignTicketValidator : AbstractValidator<AssignTicketCommand>
{
    public AssignTicketValidator() => RuleFor(x => x.Version).NotEmpty();
}

internal sealed class ChangeTicketStatusValidator : AbstractValidator<ChangeTicketStatusCommand>
{
    public ChangeTicketStatusValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Reason).MaximumLength(500);
        RuleFor(x => x.Version).NotEmpty();
    }
}

internal sealed class EscalateTicketValidator : AbstractValidator<EscalateTicketCommand>
{
    public EscalateTicketValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MinimumLength(3).MaximumLength(500);
        RuleFor(x => x.Version).NotEmpty();
    }
}

internal sealed class TicketCommandHandlers(
    IAppDbContext db, ICurrentUser currentUser, IUserDirectory users, IClock clock, TicketReader reader) :
    IRequestHandler<CreateTicketCommand, TicketDto>,
    IRequestHandler<UpdateTicketCommand, TicketDto>,
    IRequestHandler<AssignTicketCommand, TicketDto>,
    IRequestHandler<TakeTicketCommand, TicketDto>,
    IRequestHandler<ChangeTicketStatusCommand, TicketDto>,
    IRequestHandler<EscalateTicketCommand, TicketDto>
{
    public async Task<TicketDto> Handle(CreateTicketCommand r, CancellationToken ct)
    {
        var actor = currentUser.RequiredUserId;
        var customer = await db.Customers.Include(c => c.Contacts).FirstOrDefaultAsync(c => c.Id == r.CustomerId, ct)
            ?? throw Invalid("customerId", "Customer not found.");

        if (r.ContactPersonId is { } contactId && !customer.HasContact(contactId))
        {
            throw Invalid("contactPersonId", "The contact person does not belong to this customer.");
        }

        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == r.CategoryId && c.IsActive, ct)
            ?? throw Invalid("categoryId", "Category not found.");

        var departmentId = r.DepartmentId ?? category.DepartmentId ?? (currentUser.DepartmentIds.Count > 0 ? currentUser.DepartmentIds[0] : Guid.Empty);
        if (departmentId == Guid.Empty || !await db.Departments.AnyAsync(d => d.Id == departmentId && d.IsActive, ct))
        {
            throw Invalid("departmentId", "Select a department for this ticket.");
        }

        var ticket = Ticket.Create(r.Subject, r.Description, customer.Id, r.ContactPersonId, r.Channel, category.Id,
            r.Priority, departmentId, customer.BranchId ?? currentUser.BranchId, actor, clock.UtcNow);

        if (r.AssigneeId is { } assigneeId)
        {
            await EnsureCanAssign(assigneeId, departmentId, ct);
            ticket.Assign(assigneeId, actor, clock.UtcNow);
        }

        db.Tickets.Add(ticket);
        await db.SaveChangesAsync(ct);

        var created = await db.Tickets.AsNoTracking().SingleAsync(t => t.Id == ticket.Id, ct);
        return await reader.ToDto(created, ct);
    }

    public async Task<TicketDto> Handle(UpdateTicketCommand r, CancellationToken ct)
    {
        var ticket = await LoadVersioned(r.TicketId, r.Version, ct);

        if (r.CategoryId is { } categoryId && !await db.Categories.AnyAsync(c => c.Id == categoryId && c.IsActive, ct))
        {
            throw Invalid("categoryId", "Category not found.");
        }

        if (r.DepartmentId is { } departmentId && departmentId != ticket.DepartmentId)
        {
            RequirePermission(Permissions.TicketsAssign);
            if (!await db.Departments.AnyAsync(d => d.Id == departmentId && d.IsActive, ct))
            {
                throw Invalid("departmentId", "Department not found.");
            }
        }

        ticket.UpdateDetails(r.Subject ?? ticket.Subject, r.CategoryId ?? ticket.CategoryId, r.Priority ?? ticket.Priority,
            r.DepartmentId ?? ticket.DepartmentId, currentUser.RequiredUserId, clock.UtcNow);
        return await Save(ticket, ct);
    }

    public async Task<TicketDto> Handle(AssignTicketCommand r, CancellationToken ct)
    {
        RequirePermission(Permissions.TicketsAssign);
        var ticket = await LoadVersioned(r.TicketId, r.Version, ct);
        var actor = currentUser.RequiredUserId;

        if (r.DepartmentId is { } departmentId && departmentId != ticket.DepartmentId)
        {
            if (!await db.Departments.AnyAsync(d => d.Id == departmentId && d.IsActive, ct))
            {
                throw Invalid("departmentId", "Department not found.");
            }

            ticket.UpdateDetails(ticket.Subject, ticket.CategoryId, ticket.Priority, departmentId, actor, clock.UtcNow);
        }

        if (r.AssigneeId is { } assigneeId)
        {
            await EnsureCanAssign(assigneeId, ticket.DepartmentId, ct);
        }

        ticket.Assign(r.AssigneeId, actor, clock.UtcNow);
        return await Save(ticket, ct);
    }

    public async Task<TicketDto> Handle(TakeTicketCommand r, CancellationToken ct)
    {
        var ticket = await reader.LoadForUpdate(r.TicketId, ct);
        var me = currentUser.RequiredUserId;
        if (!currentUser.DepartmentIds.Contains(ticket.DepartmentId) && !currentUser.HasPermission(Permissions.TicketsAssignAnyDepartment))
        {
            throw new ForbiddenException("You can only take tickets from your own department queue.");
        }

        ticket.Take(me, clock.UtcNow);
        return await Save(ticket, ct);
    }

    public async Task<TicketDto> Handle(ChangeTicketStatusCommand r, CancellationToken ct)
    {
        var ticket = await LoadVersioned(r.TicketId, r.Version, ct);
        ticket.ChangeStatus(r.Status, currentUser.RequiredUserId, clock.UtcNow, r.Reason);
        return await Save(ticket, ct);
    }

    public async Task<TicketDto> Handle(EscalateTicketCommand r, CancellationToken ct)
    {
        var ticket = await LoadVersioned(r.TicketId, r.Version, ct);
        var ownerId = await db.Departments.Where(d => d.Id == ticket.DepartmentId).Select(d => d.EscalationOwnerId).FirstOrDefaultAsync(ct);
        ticket.Escalate(r.Reason, ownerId, currentUser.RequiredUserId, clock.UtcNow);
        return await Save(ticket, ct);
    }

    private async Task<Ticket> LoadVersioned(Guid ticketId, string version, CancellationToken ct)
    {
        var ticket = await reader.LoadForUpdate(ticketId, ct);
        db.SetExpectedVersion(ticket, Versioning.FromVersion(version));
        return ticket;
    }

    private async Task<TicketDto> Save(Ticket ticket, CancellationToken ct)
    {
        foreach (var entry in ticket.History.Where(h => h.Id == 0))
        {
            db.TicketHistory.Add(entry);
        }

        await db.SaveChangesAsync(ct);
        return await reader.ToDto(ticket, ct);
    }

    private async Task EnsureCanAssign(Guid assigneeId, Guid departmentId, CancellationToken ct)
    {
        var assignee = await users.GetAsync(assigneeId, ct);
        if (assignee is null || !assignee.IsActive)
        {
            throw Invalid("assigneeId", "The selected agent is not an active user.");
        }

        if (!assignee.IsMemberOf(departmentId) && !currentUser.HasPermission(Permissions.TicketsAssignAnyDepartment))
        {
            throw Invalid("assigneeId", "The selected agent is not a member of the ticket's department.");
        }
    }

    private void RequirePermission(string permission)
    {
        if (!currentUser.HasPermission(permission))
        {
            throw new ForbiddenException("You do not have permission to perform this action.");
        }
    }

    private static ValidationException Invalid(string property, string message) =>
        new([new FluentValidation.Results.ValidationFailure(property, message)]);
}
