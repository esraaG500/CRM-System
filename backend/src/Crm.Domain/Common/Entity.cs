namespace Crm.Domain.Common;

public abstract class Entity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public Guid Id { get; protected set; } = Guid.NewGuid();

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}

public abstract class AuditableEntity : Entity
{
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}

public interface ISoftDelete
{
    bool IsDeleted { get; }
    void MarkDeleted();
}

public interface IHasRowVersion
{
    byte[] RowVersion { get; }
}

public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}

/// <summary>Marks an entity whose changes are written to the audit log.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class AuditedAttribute : Attribute;
