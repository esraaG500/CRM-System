using System.Net.Http.Json;
using System.Text.Json;
using Crm.Api.IntegrationTests.Infrastructure;

namespace Crm.Api.IntegrationTests.Dashboard;

public class AgentDashboardTests(CrmApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Agent_dashboard_shows_my_tickets_queue_and_awaiting_reply()
    {
        var agent = await Factory.CreateClientAs(CrmApiFactory.SupportAgentEmail);
        var agentId = await Factory.UserId(CrmApiFactory.SupportAgentEmail);
        var customer = await agent.CreateCustomer();

        var urgent = await agent.CreateTicket(Factory, customer.Id(), "Urgent issue", priority: "Urgent", assigneeId: agentId);
        await agent.CreateTicket(Factory, customer.Id(), "Low issue", priority: "Low", assigneeId: agentId);
        await agent.CreateTicket(Factory, customer.Id(), "Waiting in queue");
        await agent.PostAsJsonAsync($"/api/v1/tickets/{urgent.Id()}/messages", new { body = "On it" }, Json);

        var dashboard = await agent.GetFromJsonAsync<JsonElement>("/api/v1/dashboard/agent", Json);

        dashboard.GetProperty("myOpenCount").GetInt32().ShouldBe(2);
        dashboard.GetProperty("countsByStatus").GetProperty("Open").GetInt32().ShouldBe(2);
        dashboard.GetProperty("assigned").EnumerateArray().Select(t => t.Str("subject")).ShouldBe(["Urgent issue", "Low issue"]);
        dashboard.GetProperty("awaitingFirstResponse").EnumerateArray().Select(t => t.Str("subject")).ShouldBe(["Low issue"]);
        dashboard.GetProperty("queueUnassignedCount").GetInt32().ShouldBe(1);
        dashboard.GetProperty("newToday").GetInt32().ShouldBe(3);
        dashboard.GetProperty("team").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Supervisor_dashboard_includes_team_overview()
    {
        var supervisor = await Factory.CreateClientAs(CrmApiFactory.SupervisorEmail);
        var agentId = await Factory.UserId(CrmApiFactory.SupportAgentEmail);
        var customer = await supervisor.CreateCustomer();
        await supervisor.CreateTicket(Factory, customer.Id(), "A", priority: "High", assigneeId: agentId);
        await supervisor.CreateTicket(Factory, customer.Id(), "B", priority: "High");

        var dashboard = await supervisor.GetFromJsonAsync<JsonElement>("/api/v1/dashboard/agent", Json);

        var team = dashboard.GetProperty("team");
        team.GetProperty("byPriority").GetProperty("High").GetInt32().ShouldBe(2);
        team.GetProperty("openByAgent")[0].Str("fullName").ShouldBe("Omar Al-Harbi");
    }

    [Fact]
    public async Task Lookups_return_reference_data()
    {
        var agent = await Factory.CreateClientAs(CrmApiFactory.SupportAgentEmail);

        var lookups = await agent.GetFromJsonAsync<JsonElement>("/api/v1/lookups", Json);

        lookups.GetProperty("departments").GetArrayLength().ShouldBe(3);
        lookups.GetProperty("categories").GetArrayLength().ShouldBe(5);
        lookups.GetProperty("agents").GetArrayLength().ShouldBe(5);
    }
}
