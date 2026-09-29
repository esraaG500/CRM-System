using Crm.Domain.Common;
using Microsoft.AspNetCore.Identity;

namespace Crm.Infrastructure.Identity;

public enum UserType { Staff, Customer }

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;
    public UserType UserType { get; set; } = UserType.Staff;
    public Language PreferredLanguage { get; set; } = Language.En;
    public Guid? BranchId { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsAvailable { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastSignInAt { get; set; }

    public List<UserDepartment> Departments { get; set; } = [];
}

public sealed class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole() { }

    public ApplicationRole(string name) : base(name) => NormalizedName = name.ToUpperInvariant();

    public bool IsSystem { get; set; }

    public List<RolePermission> Permissions { get; set; } = [];
}

public sealed class RolePermission
{
    public Guid RoleId { get; set; }
    public string Permission { get; set; } = string.Empty;
}

public sealed class UserDepartment
{
    public Guid UserId { get; set; }
    public Guid DepartmentId { get; set; }
}

public sealed class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? ReplacedByTokenId { get; set; }
    public string? CreatedIp { get; set; }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;
}
