using Crm.Domain.Common;

namespace Crm.Domain.Customers;

/// <summary>Free-text note attached to a customer profile.</summary>
[Audited]
public sealed class Note : AuditableEntity
{
    public const int MaxLength = 4000;

    private Note() { }

    public Note(Guid customerId, string body, Guid authorId)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            throw new DomainException("note.body-required", "Note text is required.");
        }

        if (body.Length > MaxLength)
        {
            throw new DomainException("note.too-long", $"Note must be {MaxLength} characters or fewer.");
        }

        CustomerId = customerId;
        Body = body.Trim();
        AuthorId = authorId;
    }

    public Guid CustomerId { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public Guid AuthorId { get; private set; }
}
