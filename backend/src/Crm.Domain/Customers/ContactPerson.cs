using Crm.Domain.Common;

namespace Crm.Domain.Customers;

[Audited]
public sealed class ContactPerson : AuditableEntity
{
    private ContactPerson() { }

    internal ContactPerson(Guid customerId, string name, string? email, string? phone, string? jobTitle)
    {
        CustomerId = customerId;
        Update(name, email, phone, jobTitle);
    }

    public Guid CustomerId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public string? JobTitle { get; private set; }
    public bool IsPrimary { get; private set; }

    internal void Update(string name, string? email, string? phone, string? jobTitle)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("contact.name-required", "Contact name is required.");
        }

        Name = name.Trim();
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        JobTitle = string.IsNullOrWhiteSpace(jobTitle) ? null : jobTitle.Trim();
    }

    internal void SetPrimary(bool isPrimary) => IsPrimary = isPrimary;
}
