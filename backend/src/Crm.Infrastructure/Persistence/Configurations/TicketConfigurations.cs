using Crm.Domain.Common;
using Crm.Domain.Customers;
using Crm.Domain.Organization;
using Crm.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

internal sealed class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> b)
    {
        b.ToTable("Tickets");
        b.Property(t => t.Id).ValueGeneratedNever();
        b.Property(t => t.ReferenceNumber)
            .HasMaxLength(20)
            .HasDefaultValueSql("CONCAT('TCK-', FORMAT(NEXT VALUE FOR dbo.TicketNumbers, '000000'))")
            .ValueGeneratedOnAdd();
        b.Property(t => t.ReferenceNumber).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);
        b.HasIndex(t => t.ReferenceNumber).IsUnique();
        b.Property(t => t.Subject).HasMaxLength(Ticket.SubjectMaxLength).IsRequired();
        b.Property(t => t.Description).IsRequired();
        b.Property(t => t.RowVersion).IsRowVersion();

        b.HasOne<Customer>().WithMany().HasForeignKey(t => t.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ContactPerson>().WithMany().HasForeignKey(t => t.ContactPersonId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<Category>().WithMany().HasForeignKey(t => t.CategoryId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Department>().WithMany().HasForeignKey(t => t.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Branch>().WithMany().HasForeignKey(t => t.BranchId).OnDelete(DeleteBehavior.Restrict);

        b.HasMany(t => t.History).WithOne().HasForeignKey(h => h.TicketId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(t => t.History).UsePropertyAccessMode(PropertyAccessMode.Field);

        b.HasIndex(t => new { t.DepartmentId, t.Status, t.Priority, t.CreatedAt });
        b.HasIndex(t => new { t.AssigneeId, t.Status });
        b.HasIndex(t => new { t.CustomerId, t.CreatedAt });
        b.HasQueryFilter(t => !t.IsDeleted);
    }
}

internal sealed class TicketHistoryConfiguration : IEntityTypeConfiguration<TicketHistoryEntry>
{
    public void Configure(EntityTypeBuilder<TicketHistoryEntry> b)
    {
        b.ToTable("TicketHistory");
        b.HasKey(h => h.Id);
        b.Property(h => h.Id).ValueGeneratedOnAdd();
        b.Property(h => h.Field).HasMaxLength(64);
        b.Property(h => h.OldValue).HasMaxLength(500);
        b.Property(h => h.NewValue).HasMaxLength(500);
        b.Property(h => h.Reason).HasMaxLength(500);
        b.HasIndex(h => new { h.TicketId, h.Timestamp });
    }
}

internal sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> b)
    {
        b.ToTable("Messages");
        b.Property(m => m.Id).ValueGeneratedNever();
        b.Property(m => m.Body).HasMaxLength(Message.MaxBodyLength).IsRequired();
        b.Property(m => m.DeliveryError).HasMaxLength(500);
        b.HasOne<Ticket>().WithMany().HasForeignKey(m => m.TicketId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(m => m.Mentions).WithOne().HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(m => m.Mentions).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.HasIndex(m => new { m.TicketId, m.CreatedAt });
    }
}

internal sealed class MessageMentionConfiguration : IEntityTypeConfiguration<MessageMention>
{
    public void Configure(EntityTypeBuilder<MessageMention> b)
    {
        b.ToTable("MessageMentions");
        b.HasKey(x => new { x.MessageId, x.UserId });
    }
}

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> b)
    {
        b.ToTable("Categories");
        b.Property(c => c.Id).ValueGeneratedNever();
        b.Property(c => c.NameEn).HasMaxLength(100).IsRequired();
        b.Property(c => c.NameAr).HasMaxLength(100).IsRequired();
        b.HasOne<Department>().WithMany().HasForeignKey(c => c.DepartmentId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> b)
    {
        b.ToTable("Departments");
        b.Property(d => d.Id).ValueGeneratedNever();
        b.Property(d => d.NameEn).HasMaxLength(100).IsRequired();
        b.Property(d => d.NameAr).HasMaxLength(100).IsRequired();
        b.HasIndex(d => d.NameEn).IsUnique();
    }
}

internal sealed class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> b)
    {
        b.ToTable("Branches");
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.NameEn).HasMaxLength(100).IsRequired();
        b.Property(x => x.NameAr).HasMaxLength(100).IsRequired();
        b.Property(x => x.TimeZoneId).HasMaxLength(64).IsRequired();
    }
}

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> b)
    {
        b.ToTable("AuditLog");
        b.HasKey(a => a.Id);
        b.Property(a => a.Action).HasMaxLength(32);
        b.Property(a => a.EntityType).HasMaxLength(100);
        b.Property(a => a.EntityId).HasMaxLength(64);
        b.Property(a => a.IpAddress).HasMaxLength(64);
        b.Property(a => a.CorrelationId).HasMaxLength(64);
        b.HasIndex(a => a.Timestamp);
        b.HasIndex(a => new { a.EntityType, a.EntityId });
    }
}
