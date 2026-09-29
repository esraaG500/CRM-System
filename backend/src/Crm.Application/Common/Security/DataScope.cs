using Crm.Application.Abstractions;
using Crm.Domain.Tickets;

namespace Crm.Application.Common.Security;

public static class DataScope
{
    /// <summary>Limits tickets to the user's departments unless they may see all departments.</summary>
    public static IQueryable<Ticket> VisibleTo(this IQueryable<Ticket> tickets, ICurrentUser user)
    {
        if (user.HasPermission(Permissions.DataScopeAllDepartments))
        {
            return tickets;
        }

        var departments = user.DepartmentIds.ToList();
        return tickets.Where(t => departments.Contains(t.DepartmentId) || t.AssigneeId == user.UserId);
    }

    public static bool CanSee(this ICurrentUser user, Ticket ticket) =>
        user.HasPermission(Permissions.DataScopeAllDepartments)
        || user.DepartmentIds.Contains(ticket.DepartmentId)
        || ticket.AssigneeId == user.UserId;
}
