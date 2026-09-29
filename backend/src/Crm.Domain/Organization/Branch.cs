using Crm.Domain.Common;

namespace Crm.Domain.Organization;

[Audited]
public sealed class Branch : AuditableEntity
{
    private Branch() { }

    public Branch(string nameEn, string nameAr, string timeZoneId = "Asia/Riyadh")
    {
        NameEn = nameEn.Trim();
        NameAr = nameAr.Trim();
        TimeZoneId = timeZoneId;
        IsActive = true;
    }

    public string NameEn { get; private set; } = string.Empty;
    public string NameAr { get; private set; } = string.Empty;
    public string TimeZoneId { get; private set; } = "Asia/Riyadh";
    public bool IsActive { get; private set; }
}
