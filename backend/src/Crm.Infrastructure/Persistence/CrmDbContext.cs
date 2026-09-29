using Crm.Application.Abstractions;
using Crm.Domain.Common;
using Crm.Domain.Customers;
using Crm.Domain.Organization;
using Crm.Domain.Tickets;
using Crm.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Persistence;

public sealed class CrmDbContext(DbContextOptions<CrmDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options), IAppDbContext
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<ContactPerson> ContactPeople => Set<ContactPerson>();
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketHistoryEntry> TicketHistory => Set<TicketHistoryEntry>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<AuditLogEntry> AuditLog => Set<AuditLogEntry>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserDepartment> UserDepartments => Set<UserDepartment>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public void SetExpectedVersion<TEntity>(TEntity entity, byte[] expectedVersion) where TEntity : class, IHasRowVersion =>
        Entry(entity).Property(e => e.RowVersion).OriginalValue = expectedVersion;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasSequence<int>("CustomerNumbers").StartsAt(1);
        builder.HasSequence<int>("TicketNumbers").StartsAt(1);
        builder.ApplyConfigurationsFromAssembly(typeof(CrmDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Store enums as readable names; all strings are nvarchar (Arabic-safe) by default in SQL Server.
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(32);
    }
}
