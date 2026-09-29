namespace Crm.Application.Abstractions;

/// <summary>Read access to staff user accounts (stored by the identity system in Infrastructure).</summary>
public interface IUserDirectory
{
    Task<StaffUser?> GetAsync(Guid userId, CancellationToken ct);

    Task<IReadOnlyDictionary<Guid, StaffUser>> GetManyAsync(IEnumerable<Guid> userIds, CancellationToken ct);

    Task<IReadOnlyList<StaffUser>> ListActiveStaffAsync(Guid? departmentId, CancellationToken ct);
}

public sealed record StaffUser(Guid Id, string FullName, string Email, bool IsActive, bool IsAvailable, IReadOnlyList<Guid> DepartmentIds)
{
    public bool IsMemberOf(Guid departmentId) => DepartmentIds.Contains(departmentId);
}
