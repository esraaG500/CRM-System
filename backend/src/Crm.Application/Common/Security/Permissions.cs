namespace Crm.Application.Common.Security;

/// <summary>Permission keys granted to roles. Endpoints require them via <c>[Authorize(Policy = ...)]</c>.</summary>
public static class Permissions
{
    public const string CustomersView = "customers.view";
    public const string CustomersManage = "customers.manage";
    public const string CustomersDelete = "customers.delete";
    public const string CustomersExport = "customers.export";

    public const string TicketsView = "tickets.view";
    public const string TicketsManage = "tickets.manage";
    public const string TicketsAssign = "tickets.assign";
    public const string TicketsAssignAnyDepartment = "tickets.assign.any-department";
    public const string TicketsDelete = "tickets.delete";

    public const string DashboardTeam = "dashboard.team";
    public const string ReportsView = "reports.view";

    public const string AdminUsersManage = "admin.users.manage";
    public const string AdminSettingsManage = "admin.settings.manage";
    public const string AdminAuditView = "admin.audit.view";

    /// <summary>See tickets of every department, not just the user's own.</summary>
    public const string DataScopeAllDepartments = "data.scope.all-departments";

    public static readonly IReadOnlyList<string> All =
    [
        CustomersView, CustomersManage, CustomersDelete, CustomersExport,
        TicketsView, TicketsManage, TicketsAssign, TicketsAssignAnyDepartment, TicketsDelete,
        DashboardTeam, ReportsView,
        AdminUsersManage, AdminSettingsManage, AdminAuditView,
        DataScopeAllDepartments,
    ];

    public static readonly IReadOnlyList<string> Agent =
        [CustomersView, CustomersManage, TicketsView, TicketsManage];

    public static readonly IReadOnlyList<string> Supervisor =
        [.. Agent, TicketsAssign, CustomersExport, DashboardTeam, ReportsView, DataScopeAllDepartments];
}

public static class SystemRoles
{
    public const string Administrator = "Administrator";
    public const string Supervisor = "Supervisor";
    public const string Agent = "Agent";
    public const string Customer = "Customer";
}
