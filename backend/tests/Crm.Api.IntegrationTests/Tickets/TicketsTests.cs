using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Crm.Api.IntegrationTests.Infrastructure;

namespace Crm.Api.IntegrationTests.Tickets;

public class TicketsTests(CrmApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Create_uses_category_department_and_generates_reference()
    {
        var client = await Factory.CreateClientAs(CrmApiFactory.SupportAgentEmail);
        var customer = await client.CreateCustomer();

        var ticket = await client.CreateTicket(Factory, customer.Id(), category: "Complaint", priority: "High");

        ticket.Str("referenceNumber").ShouldMatch(@"^TCK-\d{6}$");
        ticket.Str("status").ShouldBe("New");
        ticket.GetProperty("department").Str("nameEn").ShouldBe("Customer Support");
        ticket.GetProperty("customer").Str("name").ShouldBe("Acme Trading");
        ticket.GetProperty("allowedTransitions").EnumerateArray().Select(s => s.GetString()).ShouldBe(["Open", "InProgress", "Closed"]);
    }

    [Fact]
    public async Task Create_rejects_contact_of_another_customer()
    {
        var client = await Factory.CreateClientAs(CrmApiFactory.SupportAgentEmail);
        var acme = await client.CreateCustomer();
        var other = await client.CreateCustomer("Other Co", "x@other.sa");
        var contact = await (await client.PostAsJsonAsync($"/api/v1/customers/{other.Id()}/contacts", new { name = "Nasser" }, Json)).ReadJson(HttpStatusCode.Created);

        var response = await client.PostAsJsonAsync("/api/v1/tickets", new
        {
            subject = "Help",
            description = "Details",
            customerId = acme.Id(),
            contactPersonId = contact.Id(),
            categoryId = await Factory.CategoryId("General inquiry"),
        }, Json);

        (await response.ReadJson(HttpStatusCode.BadRequest)).GetProperty("errors").TryGetProperty("contactPersonId", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Supervisor_assigns_and_agent_works_ticket_to_closed_with_full_history()
    {
        var supervisor = await Factory.CreateClientAs(CrmApiFactory.SupervisorEmail);
        var agent = await Factory.CreateClientAs(CrmApiFactory.SupportAgentEmail);
        var agentId = await Factory.UserId(CrmApiFactory.SupportAgentEmail);
        var customer = await supervisor.CreateCustomer();
        var ticket = await supervisor.CreateTicket(Factory, customer.Id());
        var url = $"/api/v1/tickets/{ticket.Id()}";

        var assigned = await (await supervisor.PostAsJsonAsync($"{url}/assign", new { assigneeId = agentId, version = ticket.Str("version") }, Json)).ReadJson(HttpStatusCode.OK);
        assigned.Str("status").ShouldBe("Open");
        assigned.GetProperty("assignee").Str("fullName").ShouldBe("Omar Al-Harbi");

        var inProgress = await (await agent.PostAsJsonAsync($"{url}/status", new { status = "InProgress", version = assigned.Str("version") }, Json)).ReadJson(HttpStatusCode.OK);
        var reply = await agent.PostAsJsonAsync($"{url}/messages", new { body = "We have corrected the invoice.", setStatus = "Resolved" }, Json);
        (await reply.ReadJson(HttpStatusCode.Accepted)).Str("deliveryStatus").ShouldBe("Sent");

        var resolved = await agent.GetFromJsonAsync<JsonElement>(url, Json);
        resolved.Str("status").ShouldBe("Resolved");
        resolved.GetProperty("firstRespondedAt").ValueKind.ShouldNotBe(JsonValueKind.Null);

        var closed = await (await agent.PostAsJsonAsync($"{url}/status", new { status = "Closed", version = resolved.Str("version") }, Json)).ReadJson(HttpStatusCode.OK);
        closed.Str("status").ShouldBe("Closed");
        inProgress.Str("version").ShouldNotBe(closed.Str("version"));

        var history = await agent.GetFromJsonAsync<JsonElement>($"{url}/history", Json);
        var changes = history.EnumerateArray().Select(h => $"{h.Str("changeType")}:{(h.GetProperty("newValue").ValueKind == JsonValueKind.String ? h.Str("newValue") : "")}").ToList();
        changes.ShouldBe([
            "Created:", $"Assigned:{agentId}", "StatusChanged:Open", "StatusChanged:InProgress", "StatusChanged:Resolved", "StatusChanged:Closed",
        ]);
        history[1].GetProperty("user").Str("fullName").ShouldBe("Noura Al-Qahtani");
    }

    [Fact]
    public async Task Invalid_transition_and_stale_version_return_409()
    {
        var client = await Factory.CreateClientAs(CrmApiFactory.SupportAgentEmail);
        var customer = await client.CreateCustomer();
        var ticket = await client.CreateTicket(Factory, customer.Id());
        var url = $"/api/v1/tickets/{ticket.Id()}/status";

        var invalid = await client.PostAsJsonAsync(url, new { status = "Resolved", version = ticket.Str("version") }, Json);
        (await invalid.ReadJson(HttpStatusCode.Conflict)).Str("code").ShouldBe("ticket.invalid-transition");

        (await client.PostAsJsonAsync(url, new { status = "InProgress", version = ticket.Str("version") }, Json)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var stale = await client.PostAsJsonAsync(url, new { status = "Open", version = ticket.Str("version") }, Json);
        (await stale.ReadJson(HttpStatusCode.Conflict)).Str("code").ShouldBe("concurrency.stale");
    }

    [Fact]
    public async Task Agents_only_see_tickets_of_their_departments()
    {
        var supportAgent = await Factory.CreateClientAs(CrmApiFactory.SupportAgentEmail);
        var technicalAgent = await Factory.CreateClientAs(CrmApiFactory.TechnicalAgentEmail);
        var customer = await supportAgent.CreateCustomer();
        var supportTicket = await supportAgent.CreateTicket(Factory, customer.Id(), category: "Complaint");
        await supportAgent.CreateTicket(Factory, customer.Id(), "App crashes", category: "Technical issue");

        var technicalList = await technicalAgent.GetFromJsonAsync<JsonElement>("/api/v1/tickets", Json);
        technicalList.GetProperty("totalCount").GetInt32().ShouldBe(1);
        technicalList.GetProperty("items")[0].Str("subject").ShouldBe("App crashes");

        (await technicalAgent.GetAsync($"/api/v1/tickets/{supportTicket.Id()}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Agents_cannot_assign_but_can_take_from_their_queue()
    {
        var agent = await Factory.CreateClientAs(CrmApiFactory.SupportAgentEmail);
        var customer = await agent.CreateCustomer();
        var ticket = await agent.CreateTicket(Factory, customer.Id());

        var assign = await agent.PostAsJsonAsync($"/api/v1/tickets/{ticket.Id()}/assign",
            new { assigneeId = await Factory.UserId(CrmApiFactory.SupportAgentEmail), version = ticket.Str("version") }, Json);
        assign.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var taken = await (await agent.PostAsync($"/api/v1/tickets/{ticket.Id()}/take", null)).ReadJson(HttpStatusCode.OK);
        taken.GetProperty("assignee").Str("fullName").ShouldBe("Omar Al-Harbi");

        var again = await agent.PostAsync($"/api/v1/tickets/{ticket.Id()}/take", null);
        (await again.ReadJson(HttpStatusCode.Conflict)).Str("code").ShouldBe("ticket.already-assigned");
    }

    [Fact]
    public async Task Supervisor_cannot_assign_agent_outside_ticket_department()
    {
        var supervisor = await Factory.CreateClientAs(CrmApiFactory.SupervisorEmail);
        var customer = await supervisor.CreateCustomer();
        var ticket = await supervisor.CreateTicket(Factory, customer.Id(), category: "Complaint");

        var response = await supervisor.PostAsJsonAsync($"/api/v1/tickets/{ticket.Id()}/assign",
            new { assigneeId = await Factory.UserId(CrmApiFactory.TechnicalAgentEmail), version = ticket.Str("version") }, Json);

        (await response.ReadJson(HttpStatusCode.BadRequest)).GetProperty("errors").TryGetProperty("assigneeId", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Internal_notes_are_hidden_when_internal_messages_are_excluded()
    {
        var client = await Factory.CreateClientAs(CrmApiFactory.SupportAgentEmail);
        var colleague = await Factory.UserId("agent2@crm.local");
        var customer = await client.CreateCustomer();
        var ticket = await client.CreateTicket(Factory, customer.Id());
        var url = $"/api/v1/tickets/{ticket.Id()}";

        var note = await client.PostAsJsonAsync($"{url}/notes", new { body = "@Layla can you check billing?", mentionUserIds = new[] { colleague } }, Json);
        (await note.ReadJson(HttpStatusCode.Created)).GetProperty("mentions")[0].Str("fullName").ShouldBe("Layla Hassan");
        await client.PostAsJsonAsync($"{url}/messages", new { body = "We are looking into it." }, Json);

        var all = await client.GetFromJsonAsync<JsonElement>($"{url}/messages", Json);
        var publicOnly = await client.GetFromJsonAsync<JsonElement>($"{url}/messages?includeInternal=false", Json);

        all.GetArrayLength().ShouldBe(2);
        publicOnly.GetArrayLength().ShouldBe(1);
        publicOnly[0].GetProperty("isInternal").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Escalation_requires_reason_and_reassigns_to_escalation_owner()
    {
        var agent = await Factory.CreateClientAs(CrmApiFactory.SupportAgentEmail);
        var customer = await agent.CreateCustomer();
        var ticket = await agent.CreateTicket(Factory, customer.Id());
        var url = $"/api/v1/tickets/{ticket.Id()}/escalate";

        (await agent.PostAsJsonAsync(url, new { reason = "", version = ticket.Str("version") }, Json)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var escalated = await (await agent.PostAsJsonAsync(url, new { reason = "VIP customer waiting", version = ticket.Str("version") }, Json)).ReadJson(HttpStatusCode.OK);
        escalated.GetProperty("escalationLevel").GetInt32().ShouldBe(1);
        escalated.GetProperty("assignee").Str("fullName").ShouldBe("Noura Al-Qahtani");
    }

    [Fact]
    public async Task List_filters_by_status_priority_and_assignee()
    {
        var supervisor = await Factory.CreateClientAs(CrmApiFactory.SupervisorEmail);
        var customer = await supervisor.CreateCustomer();
        var me = await Factory.UserId(CrmApiFactory.SupervisorEmail);
        await supervisor.CreateTicket(Factory, customer.Id(), "Urgent outage", priority: "Urgent", assigneeId: me);
        await supervisor.CreateTicket(Factory, customer.Id(), "Low question", priority: "Low");

        var urgent = await supervisor.GetFromJsonAsync<JsonElement>("/api/v1/tickets?priority=Urgent&priority=High", Json);
        urgent.GetProperty("items").EnumerateArray().Select(i => i.Str("subject")).ShouldBe(["Urgent outage"]);

        var unassigned = await supervisor.GetFromJsonAsync<JsonElement>("/api/v1/tickets?assigneeId=unassigned&status=New", Json);
        unassigned.GetProperty("items").EnumerateArray().Select(i => i.Str("subject")).ShouldBe(["Low question"]);

        var mine = await supervisor.GetFromJsonAsync<JsonElement>("/api/v1/tickets?assigneeId=me&q=outage", Json);
        mine.GetProperty("totalCount").GetInt32().ShouldBe(1);
    }
}
