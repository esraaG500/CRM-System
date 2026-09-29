using Crm.Domain.Common;

namespace Crm.Application.Abstractions;

public interface ICurrentUser
{
    Guid? UserId { get; }
    bool IsAuthenticated { get; }
    IReadOnlySet<string> Permissions { get; }
    IReadOnlyList<Guid> DepartmentIds { get; }
    Guid? BranchId { get; }
    Language Language { get; }

    bool HasPermission(string permission) => Permissions.Contains(permission);

    /// <summary>Id of the signed-in user; throws when anonymous.</summary>
    Guid RequiredUserId => UserId ?? throw new UnauthorizedAccessException("Authentication required.");
}

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
