using Crm.Application.Abstractions;
using Crm.Application.Common.Messaging;
using Crm.Application.Common.Security;
using Crm.Application.Tickets;
using Crm.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Crm.Application.Dashboard;

public sealed record GetAgentDashboardQuery : IRequest<AgentDashboardDto>;

public sealed record AgentDashboardDto(
    IReadOnlyDictionary<TicketStatus, int> CountsByStatus,
    int MyOpenCount,
    IReadOnlyList<TicketSummaryDto> Assigned,
    IReadOnlyList<TicketSummaryDto> AwaitingFirstResponse,
    int QueueUnassignedCount,
    IReadOnlyList<TicketSummaryDto> UnassignedQueue,
    int NewToday,
    int ResolvedToday,
    TeamOverviewDto? Team);

public sealed record TeamOverviewDto(
    IReadOnlyDictionary<TicketStatus, int> ByStatus,
    IReadOnlyDictionary<TicketPriority, int> ByPriority,
    IReadOnlyList<AgentLoadDto> OpenByAgent);

public sealed record AgentLoadDto(Guid AgentId, string FullName, int OpenTickets);

internal sealed class GetAgentDashboardHandler(IAppDbContext db, ICurrentUser currentUser, IUserDirectory users, IClock clock, TicketReader reader)
    : IRequestHandler<GetAgentDashboardQuery, AgentDashboardDto>
{
    private const int ListSize = 10;
    private static readonly TicketStatus[] OpenStatuses = [TicketStatus.New, TicketStatus.Open, TicketStatus.InProgress, TicketStatus.PendingCustomer];

    public async Task<AgentDashboardDto> Handle(GetAgentDashboardQuery request, CancellationToken ct)
    {
        var me = currentUser.RequiredUserId;
        var visible = db.Tickets.AsNoTracking().VisibleTo(currentUser);
        var mine = visible.Where(t => t.AssigneeId == me);
        var myOpen = mine.Where(t => OpenStatuses.Contains(t.Status));

        var countsByStatus = await CountByStatus(mine, ct);

        var assigned = await reader.ToSummaries(
            myOpen.OrderByDescending(t => t.Priority).ThenBy(t => t.CreatedAt).Take(ListSize), ct);

        var awaiting = await reader.ToSummaries(
            myOpen.Where(t => t.FirstRespondedAt == null).OrderBy(t => t.CreatedAt).Take(ListSize), ct);

        var myDepartments = currentUser.DepartmentIds.ToList();
        var queue = visible.Where(t => t.AssigneeId == null && OpenStatuses.Contains(t.Status) && myDepartments.Contains(t.DepartmentId));
        var queueCount = await queue.CountAsync(ct);
        var queueTop = await reader.ToSummaries(queue.OrderByDescending(t => t.Priority).ThenBy(t => t.CreatedAt).Take(ListSize), ct);

        var startOfDay = new DateTimeOffset(clock.UtcNow.UtcDateTime.Date, TimeSpan.Zero);
        var newToday = await visible.CountAsync(t => t.CreatedAt >= startOfDay, ct);
        var resolvedToday = await visible.CountAsync(t => t.ResolvedAt >= startOfDay, ct);

        TeamOverviewDto? team = null;
        if (currentUser.HasPermission(Permissions.DashboardTeam))
        {
            var open = visible.Where(t => OpenStatuses.Contains(t.Status));
            var byPriority = await open.GroupBy(t => t.Priority).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
            var byAgent = await open.Where(t => t.AssigneeId != null)
                .GroupBy(t => t.AssigneeId!.Value).Select(g => new { AgentId = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count).Take(20).ToListAsync(ct);
            var names = await users.GetManyAsync(byAgent.Select(a => a.AgentId), ct);

            team = new TeamOverviewDto(
                await CountByStatus(visible, ct),
                Enum.GetValues<TicketPriority>().ToDictionary(p => p, p => byPriority.FirstOrDefault(x => x.Key == p)?.Count ?? 0),
                byAgent.Select(a => new AgentLoadDto(a.AgentId, names.TryGetValue(a.AgentId, out var u) ? u.FullName : string.Empty, a.Count)).ToList());
        }

        return new AgentDashboardDto(
            countsByStatus, countsByStatus.Where(kv => OpenStatuses.Contains(kv.Key)).Sum(kv => kv.Value),
            assigned, awaiting, queueCount, queueTop, newToday, resolvedToday, team);
    }

    private static async Task<IReadOnlyDictionary<TicketStatus, int>> CountByStatus(IQueryable<Domain.Tickets.Ticket> tickets, CancellationToken ct)
    {
        var rows = await tickets.GroupBy(t => t.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        return Enum.GetValues<TicketStatus>().ToDictionary(s => s, s => rows.FirstOrDefault(r => r.Key == s)?.Count ?? 0);
    }
}
