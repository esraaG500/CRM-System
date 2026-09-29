using Crm.Application.Common.Security;
using Crm.Application.Dashboard;
using Crm.Application.Lookups;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Controllers.V1;

[Route("api/v{version:apiVersion}")]
public sealed class DashboardController : ApiControllerBase
{
    [HttpGet("dashboard/agent")]
    [Authorize(Policy = Permissions.TicketsView)]
    public Task<AgentDashboardDto> Agent(CancellationToken ct) => Dispatcher.Send(new GetAgentDashboardQuery(), ct);

    [HttpGet("lookups")]
    public Task<LookupsDto> Lookups(CancellationToken ct) => Dispatcher.Send(new GetLookupsQuery(), ct);
}
