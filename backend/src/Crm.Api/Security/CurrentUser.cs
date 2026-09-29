using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Crm.Application.Abstractions;
using Crm.Domain.Common;
using Crm.Infrastructure.Identity;
using Crm.Infrastructure.Persistence.Interceptors;

namespace Crm.Api.Security;

/// <summary>Reads the signed-in user from the JWT claims of the current request.</summary>
internal sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser, IAuditContext
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public Guid? UserId =>
        Guid.TryParse(Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) ? id : null;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public IReadOnlySet<string> Permissions =>
        Principal?.FindAll(CrmClaims.Permission).Select(c => c.Value).ToHashSet(StringComparer.Ordinal) ?? [];

    public IReadOnlyList<Guid> DepartmentIds =>
        Principal?.FindAll(CrmClaims.Department).Select(c => Guid.TryParse(c.Value, out var d) ? d : Guid.Empty)
            .Where(d => d != Guid.Empty).ToList() ?? [];

    public Guid? BranchId => Guid.TryParse(Principal?.FindFirstValue(CrmClaims.Branch), out var b) ? b : null;

    public Language Language =>
        Enum.TryParse<Language>(Principal?.FindFirstValue(CrmClaims.Language), ignoreCase: true, out var l) ? l : Language.Ar;

    public string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? CorrelationId => accessor.HttpContext?.TraceIdentifier;
}
