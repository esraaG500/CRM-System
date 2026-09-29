using Crm.Application.Common.Paging;
using Crm.Application.Common.Security;
using Crm.Application.Tickets;
using Crm.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Controllers.V1;

public sealed record TicketCreateRequest(
    string Subject,
    string Description,
    Guid CustomerId,
    Guid? ContactPersonId,
    Channel? Channel,
    Guid CategoryId,
    TicketPriority? Priority,
    Guid? DepartmentId,
    Guid? AssigneeId);

public sealed record TicketUpdateRequest(string? Subject, Guid? CategoryId, TicketPriority? Priority, Guid? DepartmentId, string Version);

public sealed record AssignRequest(Guid? AssigneeId, Guid? DepartmentId, string Version);

public sealed record StatusChangeRequest(TicketStatus Status, string? Reason, string Version);

public sealed record EscalateRequest(string Reason, string Version);

public sealed record ReplyCreateRequest(string Body, TicketStatus? SetStatus);

public sealed record InternalNoteCreateRequest(string Body, IReadOnlyList<Guid>? MentionUserIds);

[Route("api/v{version:apiVersion}/tickets")]
[Authorize(Policy = Permissions.TicketsView)]
public sealed class TicketsController : ApiControllerBase
{
    [HttpGet]
    public Task<PagedResult<TicketSummaryDto>> List(
        [FromQuery] string? q,
        [FromQuery] TicketStatus[]? status,
        [FromQuery] TicketPriority[]? priority,
        [FromQuery] Guid? categoryId,
        [FromQuery] Guid? departmentId,
        [FromQuery] Guid? branchId,
        [FromQuery] string? assigneeId,
        [FromQuery] Guid? customerId,
        [FromQuery] Channel? channel,
        [FromQuery] DateOnly? createdFrom,
        [FromQuery] DateOnly? createdTo,
        [FromQuery] string? sort,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PageRequest.DefaultPageSize,
        CancellationToken ct = default)
    {
        var order = sort switch
        {
            "createdAt" => TicketSort.CreatedAt,
            "priority" => TicketSort.Priority,
            "-updatedAt" => TicketSort.UpdatedAtDesc,
            _ => TicketSort.CreatedAtDesc,
        };

        return Dispatcher.Send(new ListTicketsQuery(q, status, priority, categoryId, departmentId, branchId, assigneeId,
            customerId, channel, createdFrom, createdTo, order, new PageRequest(page, pageSize)), ct);
    }

    [HttpPost]
    [Authorize(Policy = Permissions.TicketsManage)]
    public async Task<ActionResult<TicketDto>> Create(TicketCreateRequest r, CancellationToken ct)
    {
        var ticket = await Dispatcher.Send(new CreateTicketCommand(r.Subject, r.Description, r.CustomerId, r.ContactPersonId,
            r.Channel ?? Channel.Phone, r.CategoryId, r.Priority ?? TicketPriority.Medium, r.DepartmentId, r.AssigneeId), ct);
        return CreatedAtAction(nameof(Get), new { ticketId = ticket.Id, version = "1" }, ticket);
    }

    [HttpGet("{ticketId:guid}")]
    public Task<TicketDto> Get(Guid ticketId, CancellationToken ct) => Dispatcher.Send(new GetTicketQuery(ticketId), ct);

    [HttpPatch("{ticketId:guid}")]
    [Authorize(Policy = Permissions.TicketsManage)]
    public Task<TicketDto> Update(Guid ticketId, TicketUpdateRequest r, CancellationToken ct) =>
        Dispatcher.Send(new UpdateTicketCommand(ticketId, r.Subject, r.CategoryId, r.Priority, r.DepartmentId, r.Version), ct);

    [HttpPost("{ticketId:guid}/assign")]
    [Authorize(Policy = Permissions.TicketsAssign)]
    public Task<TicketDto> Assign(Guid ticketId, AssignRequest r, CancellationToken ct) =>
        Dispatcher.Send(new AssignTicketCommand(ticketId, r.AssigneeId, r.DepartmentId, r.Version), ct);

    [HttpPost("{ticketId:guid}/take")]
    [Authorize(Policy = Permissions.TicketsManage)]
    public Task<TicketDto> Take(Guid ticketId, CancellationToken ct) => Dispatcher.Send(new TakeTicketCommand(ticketId), ct);

    [HttpPost("{ticketId:guid}/status")]
    [Authorize(Policy = Permissions.TicketsManage)]
    public Task<TicketDto> ChangeStatus(Guid ticketId, StatusChangeRequest r, CancellationToken ct) =>
        Dispatcher.Send(new ChangeTicketStatusCommand(ticketId, r.Status, r.Reason, r.Version), ct);

    [HttpPost("{ticketId:guid}/escalate")]
    [Authorize(Policy = Permissions.TicketsManage)]
    public Task<TicketDto> Escalate(Guid ticketId, EscalateRequest r, CancellationToken ct) =>
        Dispatcher.Send(new EscalateTicketCommand(ticketId, r.Reason, r.Version), ct);

    [HttpGet("{ticketId:guid}/history")]
    public Task<IReadOnlyList<TicketHistoryEntryDto>> History(Guid ticketId, CancellationToken ct) =>
        Dispatcher.Send(new GetTicketHistoryQuery(ticketId), ct);

    [HttpGet("{ticketId:guid}/messages")]
    public Task<IReadOnlyList<MessageDto>> Messages(Guid ticketId, [FromQuery] bool includeInternal = true, CancellationToken ct = default) =>
        Dispatcher.Send(new ListMessagesQuery(ticketId, includeInternal), ct);

    [HttpPost("{ticketId:guid}/messages")]
    [Authorize(Policy = Permissions.TicketsManage)]
    public async Task<ActionResult<MessageDto>> Reply(Guid ticketId, ReplyCreateRequest r, CancellationToken ct)
    {
        var message = await Dispatcher.Send(new ReplyToTicketCommand(ticketId, r.Body, r.SetStatus), ct);
        return Accepted(message);
    }

    [HttpPost("{ticketId:guid}/notes")]
    [Authorize(Policy = Permissions.TicketsManage)]
    public async Task<ActionResult<MessageDto>> AddNote(Guid ticketId, InternalNoteCreateRequest r, CancellationToken ct)
    {
        var note = await Dispatcher.Send(new AddInternalNoteCommand(ticketId, r.Body, r.MentionUserIds), ct);
        return StatusCode(StatusCodes.Status201Created, note);
    }
}
