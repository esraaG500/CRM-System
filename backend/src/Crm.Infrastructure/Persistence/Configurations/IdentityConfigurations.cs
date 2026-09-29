using Crm.Domain.Organization;
using Crm.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

internal sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> b)
    {
        b.Property(u => u.FullName).HasMaxLength(150).IsRequired();
        b.HasOne<Branch>().WithMany().HasForeignKey(u => u.BranchId).OnDelete(DeleteBehavior.SetNull);
        b.HasMany(u => u.Departments).WithOne().HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ApplicationRoleConfiguration : IEntityTypeConfiguration<ApplicationRole>
{
    public void Configure(EntityTypeBuilder<ApplicationRole> b) =>
        b.HasMany(r => r.Permissions).WithOne().HasForeignKey(p => p.RoleId).OnDelete(DeleteBehavior.Cascade);
}

internal sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> b)
    {
        b.ToTable("RolePermissions");
        b.HasKey(p => new { p.RoleId, p.Permission });
        b.Property(p => p.Permission).HasMaxLength(100);
    }
}

internal sealed class UserDepartmentConfiguration : IEntityTypeConfiguration<UserDepartment>
{
    public void Configure(EntityTypeBuilder<UserDepartment> b)
    {
        b.ToTable("UserDepartments");
        b.HasKey(d => new { d.UserId, d.DepartmentId });
        b.HasOne<Department>().WithMany().HasForeignKey(d => d.DepartmentId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("RefreshTokens");
        b.Property(t => t.TokenHash).HasMaxLength(128).IsRequired();
        b.Property(t => t.CreatedIp).HasMaxLength(64);
        b.HasIndex(t => t.TokenHash).IsUnique();
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
