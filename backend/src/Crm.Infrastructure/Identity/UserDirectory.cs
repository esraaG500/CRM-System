using Crm.Application.Abstractions;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Identity;

internal sealed class UserDirectory(CrmDbContext db) : IUserDirectory
{
    public async Task<StaffUser?> GetAsync(Guid userId, CancellationToken ct) =>
        (await GetManyAsync([userId], ct)).GetValueOrDefault(userId);

    public async Task<IReadOnlyDictionary<Guid, StaffUser>> GetManyAsync(IEnumerable<Guid> userIds, CancellationToken ct)
    {
        var ids = userIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, StaffUser>();
        }

        var users = await Staff().Where(u => ids.Contains(u.Id)).ToListAsync(ct);
        return users.Select(Map).ToDictionary(u => u.Id);
    }

    public async Task<IReadOnlyList<StaffUser>> ListActiveStaffAsync(Guid? departmentId, CancellationToken ct)
    {
        var query = Staff().Where(u => u.IsActive);
        if (departmentId is { } dept)
        {
            query = query.Where(u => u.Departments.Any(d => d.DepartmentId == dept));
        }

        var users = await query.OrderBy(u => u.FullName).ToListAsync(ct);
        return users.Select(Map).ToList();
    }

    private IQueryable<ApplicationUser> Staff() =>
        db.Users.AsNoTracking().Include(u => u.Departments).Where(u => u.UserType == UserType.Staff);

    private static StaffUser Map(ApplicationUser u) =>
        new(u.Id, u.FullName, u.Email ?? string.Empty, u.IsActive, u.IsAvailable, u.Departments.Select(d => d.DepartmentId).ToList());
}
