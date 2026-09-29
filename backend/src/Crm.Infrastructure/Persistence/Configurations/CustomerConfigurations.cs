using Crm.Domain.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> b)
    {
        b.ToTable("Customers");
        b.Property(c => c.Id).ValueGeneratedNever();
        b.Property(c => c.ReferenceNumber)
            .HasMaxLength(20)
            .HasDefaultValueSql("CONCAT('CUS-', FORMAT(NEXT VALUE FOR dbo.CustomerNumbers, '000000'))")
            .ValueGeneratedOnAdd();
        b.Property(c => c.ReferenceNumber).Metadata.SetAfterSaveBehavior(Microsoft.EntityFrameworkCore.Metadata.PropertySaveBehavior.Ignore);
        b.HasIndex(c => c.ReferenceNumber).IsUnique();
        b.Property(c => c.Name).HasMaxLength(200).IsRequired();
        b.Property(c => c.PrimaryEmail).HasMaxLength(256);
        b.Property(c => c.PrimaryPhone).HasMaxLength(20);
        b.Property(c => c.ErpReference).HasMaxLength(64);
        b.Property(c => c.RowVersion).IsRowVersion();
        b.OwnsOne(c => c.Address, a =>
        {
            a.Property(x => x.Line1).HasColumnName("AddressLine1").HasMaxLength(200);
            a.Property(x => x.City).HasColumnName("AddressCity").HasMaxLength(100);
            a.Property(x => x.Country).HasColumnName("AddressCountry").HasMaxLength(2);
        });
        b.HasMany(c => c.Contacts).WithOne().HasForeignKey(p => p.CustomerId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(c => c.Contacts).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.HasIndex(c => c.Name);
        b.HasIndex(c => c.PrimaryEmail);
        b.HasIndex(c => c.PrimaryPhone);
        b.HasQueryFilter(c => !c.IsDeleted);
    }
}

internal sealed class ContactPersonConfiguration : IEntityTypeConfiguration<ContactPerson>
{
    public void Configure(EntityTypeBuilder<ContactPerson> b)
    {
        b.ToTable("ContactPeople");
        b.Property(p => p.Id).ValueGeneratedNever();
        b.Property(p => p.Name).HasMaxLength(150).IsRequired();
        b.Property(p => p.Email).HasMaxLength(256);
        b.Property(p => p.Phone).HasMaxLength(20);
        b.Property(p => p.JobTitle).HasMaxLength(100);
    }
}

internal sealed class NoteConfiguration : IEntityTypeConfiguration<Note>
{
    public void Configure(EntityTypeBuilder<Note> b)
    {
        b.ToTable("Notes");
        b.Property(n => n.Id).ValueGeneratedNever();
        b.Property(n => n.Body).HasMaxLength(Note.MaxLength).IsRequired();
        b.HasOne<Customer>().WithMany().HasForeignKey(n => n.CustomerId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(n => new { n.CustomerId, n.CreatedAt });
    }
}
