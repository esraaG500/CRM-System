using Crm.Application.Abstractions;
using Crm.Application.Common;
using Crm.Application.Common.Messaging;
using Crm.Domain.Customers;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Crm.Application.Customers;

public sealed record GetCustomerQuery(Guid CustomerId) : IRequest<CustomerDto>;

public sealed record CreateCustomerCommand(CustomerUpsertDto Customer) : IRequest<CustomerDto>;

public sealed record UpdateCustomerCommand(Guid CustomerId, CustomerUpsertDto Customer) : IRequest<CustomerDto>;

public sealed record DeactivateCustomerCommand(Guid CustomerId) : IRequest<bool>;

internal sealed class CreateCustomerValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerValidator() => RuleFor(x => x.Customer).NotNull().SetValidator(new CustomerUpsertValidator());
}

internal sealed class UpdateCustomerValidator : AbstractValidator<UpdateCustomerCommand>
{
    public UpdateCustomerValidator()
    {
        RuleFor(x => x.Customer).NotNull().SetValidator(new CustomerUpsertValidator());
        RuleFor(x => x.Customer.Version).NotEmpty().WithName("version");
    }
}

internal sealed class CustomerHandlers(IAppDbContext db) :
    IRequestHandler<GetCustomerQuery, CustomerDto>,
    IRequestHandler<CreateCustomerCommand, CustomerDto>,
    IRequestHandler<UpdateCustomerCommand, CustomerDto>,
    IRequestHandler<DeactivateCustomerCommand, bool>
{
    public async Task<CustomerDto> Handle(GetCustomerQuery request, CancellationToken ct) =>
        (await Load(request.CustomerId, ct)).ToDto();

    public async Task<CustomerDto> Handle(CreateCustomerCommand request, CancellationToken ct)
    {
        var dto = request.Customer;
        var customer = new Customer(dto.Type, dto.Name, dto.PrimaryEmail, dto.PrimaryPhone, dto.PreferredLanguage, dto.BranchId);
        customer.UpdateProfile(dto.Type, dto.PreferredLanguage, dto.BranchId, dto.Address.ToAddress(), dto.ErpReference);

        db.Customers.Add(customer);
        await db.SaveChangesAsync(ct);

        // Reload so the database-generated reference number and row version are returned.
        return (await Load(customer.Id, ct)).ToDto();
    }

    public async Task<CustomerDto> Handle(UpdateCustomerCommand request, CancellationToken ct)
    {
        var dto = request.Customer;
        var customer = await Load(request.CustomerId, ct);
        db.SetExpectedVersion(customer, Versioning.FromVersion(dto.Version!));

        customer.UpdateDetails(dto.Name, dto.PrimaryEmail, dto.PrimaryPhone);
        customer.UpdateProfile(dto.Type, dto.PreferredLanguage, dto.BranchId, dto.Address.ToAddress(), dto.ErpReference);
        customer.MarkReviewed();

        await db.SaveChangesAsync(ct);
        return customer.ToDto();
    }

    public async Task<bool> Handle(DeactivateCustomerCommand request, CancellationToken ct)
    {
        var customer = await Load(request.CustomerId, ct);
        customer.MarkDeleted();
        await db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<Customer> Load(Guid id, CancellationToken ct) =>
        await db.Customers.Include(c => c.Contacts).FirstOrDefaultAsync(c => c.Id == id, ct)
        ?? throw new NotFoundException("Customer", id);
}
