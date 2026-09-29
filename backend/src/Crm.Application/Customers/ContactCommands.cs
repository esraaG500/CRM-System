using Crm.Application.Abstractions;
using Crm.Application.Common;
using Crm.Application.Common.Messaging;
using Crm.Domain.Customers;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Crm.Application.Customers;

public sealed record ListContactsQuery(Guid CustomerId) : IRequest<IReadOnlyList<ContactPersonDto>>;

public sealed record AddContactCommand(Guid CustomerId, ContactPersonUpsertDto Contact) : IRequest<ContactPersonDto>;

public sealed record UpdateContactCommand(Guid CustomerId, Guid ContactId, ContactPersonUpsertDto Contact) : IRequest<ContactPersonDto>;

public sealed record RemoveContactCommand(Guid CustomerId, Guid ContactId) : IRequest<bool>;

internal sealed class AddContactValidator : AbstractValidator<AddContactCommand>
{
    public AddContactValidator() => RuleFor(x => x.Contact).NotNull().SetValidator(new ContactPersonUpsertValidator());
}

internal sealed class UpdateContactValidator : AbstractValidator<UpdateContactCommand>
{
    public UpdateContactValidator() => RuleFor(x => x.Contact).NotNull().SetValidator(new ContactPersonUpsertValidator());
}

internal sealed class ContactHandlers(IAppDbContext db) :
    IRequestHandler<ListContactsQuery, IReadOnlyList<ContactPersonDto>>,
    IRequestHandler<AddContactCommand, ContactPersonDto>,
    IRequestHandler<UpdateContactCommand, ContactPersonDto>,
    IRequestHandler<RemoveContactCommand, bool>
{
    public async Task<IReadOnlyList<ContactPersonDto>> Handle(ListContactsQuery request, CancellationToken ct) =>
        (await Load(request.CustomerId, ct)).ToDto().Contacts;

    public async Task<ContactPersonDto> Handle(AddContactCommand request, CancellationToken ct)
    {
        var customer = await Load(request.CustomerId, ct);
        var c = request.Contact;
        var contact = customer.AddContact(c.Name, c.Email, c.Phone, c.JobTitle, c.IsPrimary);
        db.ContactPeople.Add(contact);
        await db.SaveChangesAsync(ct);
        return contact.ToDto();
    }

    public async Task<ContactPersonDto> Handle(UpdateContactCommand request, CancellationToken ct)
    {
        var customer = await Load(request.CustomerId, ct);
        var c = request.Contact;
        customer.UpdateContact(request.ContactId, c.Name, c.Email, c.Phone, c.JobTitle, c.IsPrimary);
        await db.SaveChangesAsync(ct);
        return customer.Contacts.Single(x => x.Id == request.ContactId).ToDto();
    }

    public async Task<bool> Handle(RemoveContactCommand request, CancellationToken ct)
    {
        var customer = await Load(request.CustomerId, ct);
        if (!customer.HasContact(request.ContactId))
        {
            throw new NotFoundException("Contact", request.ContactId);
        }

        var contact = customer.Contacts.Single(x => x.Id == request.ContactId);
        customer.RemoveContact(request.ContactId);
        db.ContactPeople.Remove(contact);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<Customer> Load(Guid id, CancellationToken ct) =>
        await db.Customers.Include(c => c.Contacts).FirstOrDefaultAsync(c => c.Id == id, ct)
        ?? throw new NotFoundException("Customer", id);
}
