using Crm.Application.Abstractions;
using Crm.Application.Common.Messaging;
using Crm.Application.Tickets;
using Microsoft.EntityFrameworkCore;

namespace Crm.Application.Lookups;

/// <summary>Reference data the staff app needs for forms and filters.</summary>
public sealed record GetLookupsQuery : IRequest<LookupsDto>;

public sealed record CategoryLookupDto(Guid Id, string NameEn, string NameAr, Guid? DepartmentId);

public sealed record AgentLookupDto(Guid Id, string FullName, bool IsAvailable, IReadOnlyList<Guid> DepartmentIds);

public sealed record LookupsDto(
    IReadOnlyList<NamedRefDto> Departments,
    IReadOnlyList<NamedRefDto> Branches,
    IReadOnlyList<CategoryLookupDto> Categories,
    IReadOnlyList<AgentLookupDto> Agents);

internal sealed class GetLookupsHandler(IAppDbContext db, IUserDirectory users) : IRequestHandler<GetLookupsQuery, LookupsDto>
{
    public async Task<LookupsDto> Handle(GetLookupsQuery request, CancellationToken ct)
    {
        var departments = await db.Departments.AsNoTracking().Where(d => d.IsActive).OrderBy(d => d.NameEn)
            .Select(d => new NamedRefDto(d.Id, d.NameEn, d.NameAr)).ToListAsync(ct);
        var branches = await db.Branches.AsNoTracking().Where(b => b.IsActive).OrderBy(b => b.NameEn)
            .Select(b => new NamedRefDto(b.Id, b.NameEn, b.NameAr)).ToListAsync(ct);
        var categories = await db.Categories.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.SortOrder).ThenBy(c => c.NameEn)
            .Select(c => new CategoryLookupDto(c.Id, c.NameEn, c.NameAr, c.DepartmentId)).ToListAsync(ct);
        var agents = (await users.ListActiveStaffAsync(null, ct))
            .Select(u => new AgentLookupDto(u.Id, u.FullName, u.IsAvailable, u.DepartmentIds)).ToList();

        return new LookupsDto(departments, branches, categories, agents);
    }
}
