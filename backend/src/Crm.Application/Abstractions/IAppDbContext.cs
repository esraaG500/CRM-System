using Crm.Domain.Common;
using Crm.Domain.Customers;
using Crm.Domain.Organization;
using Crm.Domain.Tickets;
using Microsoft.EntityFrameworkCore;

namespace Crm.Application.Abstractions;

/// <summary>Unit of work over the CRM database. Implemented in Infrastructure.</summary>
public interface IAppDbContext
{
    DbSet<Customer> Customers { get; }
    DbSet<ContactPerson> ContactPeople { get; }
    DbSet<Note> Notes { get; }
    DbSet<Ticket> Tickets { get; }
    DbSet<TicketHistoryEntry> TicketHistory { get; }
    DbSet<Message> Messages { get; }
    DbSet<Category> Categories { get; }
    DbSet<Department> Departments { get; }
    DbSet<Branch> Branches { get; }
    DbSet<AuditLogEntry> AuditLog { get; }

    /// <summary>Makes the next save fail with a concurrency conflict if the stored version differs.</summary>
    void SetExpectedVersion<TEntity>(TEntity entity, byte[] expectedVersion) where TEntity : class, IHasRowVersion;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
