using Crm.Application.Common.Paging;
using Crm.Application.Common.Security;
using Crm.Application.Customers;
using Crm.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Controllers.V1;

public sealed record NoteCreateRequest(string Body);

[Route("api/v{version:apiVersion}/customers")]
[Authorize(Policy = Permissions.CustomersView)]
public sealed class CustomersController : ApiControllerBase
{
    [HttpGet]
    public Task<PagedResult<CustomerSummaryDto>> Search(
        [FromQuery] string? q, [FromQuery] CustomerType? type, [FromQuery] Guid? branchId, [FromQuery] bool? needsReview,
        [FromQuery] int page = 1, [FromQuery] int pageSize = PageRequest.DefaultPageSize, CancellationToken ct = default) =>
        Dispatcher.Send(new SearchCustomersQuery(q, type, branchId, needsReview, new PageRequest(page, pageSize)), ct);

    [HttpPost]
    [Authorize(Policy = Permissions.CustomersManage)]
    public async Task<ActionResult<CustomerDto>> Create(CustomerUpsertDto request, CancellationToken ct)
    {
        var customer = await Dispatcher.Send(new CreateCustomerCommand(request), ct);
        return CreatedAtAction(nameof(Get), new { customerId = customer.Id, version = "1" }, customer);
    }

    [HttpGet("{customerId:guid}")]
    public Task<CustomerDto> Get(Guid customerId, CancellationToken ct) =>
        Dispatcher.Send(new GetCustomerQuery(customerId), ct);

    [HttpPut("{customerId:guid}")]
    [Authorize(Policy = Permissions.CustomersManage)]
    public Task<CustomerDto> Update(Guid customerId, CustomerUpsertDto request, CancellationToken ct) =>
        Dispatcher.Send(new UpdateCustomerCommand(customerId, request), ct);

    [HttpDelete("{customerId:guid}")]
    [Authorize(Policy = Permissions.CustomersDelete)]
    public async Task<IActionResult> Deactivate(Guid customerId, CancellationToken ct)
    {
        await Dispatcher.Send(new DeactivateCustomerCommand(customerId), ct);
        return NoContent();
    }

    [HttpGet("{customerId:guid}/contacts")]
    public Task<IReadOnlyList<ContactPersonDto>> Contacts(Guid customerId, CancellationToken ct) =>
        Dispatcher.Send(new ListContactsQuery(customerId), ct);

    [HttpPost("{customerId:guid}/contacts")]
    [Authorize(Policy = Permissions.CustomersManage)]
    public async Task<ActionResult<ContactPersonDto>> AddContact(Guid customerId, ContactPersonUpsertDto request, CancellationToken ct)
    {
        var contact = await Dispatcher.Send(new AddContactCommand(customerId, request), ct);
        return StatusCode(StatusCodes.Status201Created, contact);
    }

    [HttpPut("{customerId:guid}/contacts/{contactId:guid}")]
    [Authorize(Policy = Permissions.CustomersManage)]
    public Task<ContactPersonDto> UpdateContact(Guid customerId, Guid contactId, ContactPersonUpsertDto request, CancellationToken ct) =>
        Dispatcher.Send(new UpdateContactCommand(customerId, contactId, request), ct);

    [HttpDelete("{customerId:guid}/contacts/{contactId:guid}")]
    [Authorize(Policy = Permissions.CustomersManage)]
    public async Task<IActionResult> RemoveContact(Guid customerId, Guid contactId, CancellationToken ct)
    {
        await Dispatcher.Send(new RemoveContactCommand(customerId, contactId), ct);
        return NoContent();
    }

    [HttpGet("{customerId:guid}/timeline")]
    public Task<PagedResult<TimelineItemDto>> Timeline(
        Guid customerId, [FromQuery] int page = 1, [FromQuery] int pageSize = PageRequest.DefaultPageSize, CancellationToken ct = default) =>
        Dispatcher.Send(new GetCustomerTimelineQuery(customerId, new PageRequest(page, pageSize)), ct);

    [HttpPost("{customerId:guid}/notes")]
    [Authorize(Policy = Permissions.CustomersManage)]
    public async Task<ActionResult<NoteDto>> AddNote(Guid customerId, NoteCreateRequest request, CancellationToken ct)
    {
        var note = await Dispatcher.Send(new AddCustomerNoteCommand(customerId, request.Body), ct);
        return StatusCode(StatusCodes.Status201Created, note);
    }
}
