using Crm.Domain.Common;

namespace Crm.Domain.Tickets;

[Audited]
public sealed class Category : AuditableEntity
{
    private Category() { }

    public Category(string nameEn, string nameAr, Guid? departmentId, int sortOrder = 0)
    {
        NameEn = nameEn.Trim();
        NameAr = nameAr.Trim();
        DepartmentId = departmentId;
        SortOrder = sortOrder;
        IsActive = true;
    }

    public string NameEn { get; private set; } = string.Empty;
    public string NameAr { get; private set; } = string.Empty;

    /// <summary>Default routing department for new tickets in this category.</summary>
    public Guid? DepartmentId { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; }
}
