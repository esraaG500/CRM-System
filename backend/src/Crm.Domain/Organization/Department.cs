using Crm.Domain.Common;

namespace Crm.Domain.Organization;

[Audited]
public sealed class Department : AuditableEntity
{
    private Department() { }

    public Department(string nameEn, string nameAr)
    {
        Rename(nameEn, nameAr);
        IsActive = true;
    }

    public string NameEn { get; private set; } = string.Empty;
    public string NameAr { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public Guid? EscalationOwnerId { get; private set; }

    public void Rename(string nameEn, string nameAr)
    {
        NameEn = nameEn.Trim();
        NameAr = nameAr.Trim();
    }

    public void SetEscalationOwner(Guid? userId) => EscalationOwnerId = userId;

    public void SetActive(bool isActive) => IsActive = isActive;
}
