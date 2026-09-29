using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Crm.Api.IntegrationTests.Customers;

public class CustomersTests(CrmApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Create_returns_201_with_generated_reference_and_version()
    {
        var client = await Factory.CreateClientAs(CrmApiFactory.SupportAgentEmail);

        var customer = await client.CreateCustomer("شركة النخبة", "info@elite.sa", "+966501234567");

        customer.Str("referenceNumber").ShouldMatch(@"^CUS-\d{6}$");
        customer.Str("name").ShouldBe("شركة النخبة");
        customer.Str("preferredLanguage").ShouldBe("ar");
        customer.Str("version").ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Create_validates_input()
    {
        var client = await Factory.CreateClientAs(CrmApiFactory.SupportAgentEmail);

        var response = await client.PostAsJsonAsync("/api/v1/customers",
            new { type = "Individual", name = "", primaryEmail = "not-an-email", primaryPhone = "0501234567", preferredLanguage = "en" }, Json);

        var problem = await response.ReadJson(HttpStatusCode.BadRequest);
        var errors = problem.GetProperty("errors");
        errors.TryGetProperty("name", out _).ShouldBeTrue();
        errors.TryGetProperty("primaryEmail", out _).ShouldBeTrue();
        errors.TryGetProperty("primaryPhone", out _).ShouldBeTrue();
    }

    [Theory]
    [InlineData("acme")]
    [InlineData("INFO@ACME")]
    [InlineData("+96650111")]
    public async Task Search_matches_name_email_and_phone(string q)
    {
        var client = await Factory.CreateClientAs(CrmApiFactory.SupportAgentEmail);
        await client.CreateCustomer("Acme Trading", "info@acme.sa", "+966501112222");
        await client.CreateCustomer("Other Co", "hello@other.sa");

        var page = await client.GetFromJsonAsync<JsonElement>($"/api/v1/customers?q={Uri.EscapeDataString(q)}", Json);

        page.GetProperty("totalCount").GetInt32().ShouldBe(1);
        page.GetProperty("items")[0].Str("name").ShouldBe("Acme Trading");
    }

    [Fact]
    public async Task Search_by_reference_number()
    {
        var client = await Factory.CreateClientAs(CrmApiFactory.SupportAgentEmail);
        var created = await client.CreateCustomer();

        var page = await client.GetFromJsonAsync<JsonElement>($"/api/v1/customers?q={created.Str("referenceNumber").ToLowerInvariant()}", Json);

        page.GetProperty("items")[0].Id().ShouldBe(created.Id());
    }

    [Fact]
    public async Task Update_with_stale_version_returns_409()
    {
        var client = await Factory.CreateClientAs(CrmApiFactory.SupportAgentEmail);
        var customer = await client.CreateCustomer();
        var original = customer.Str("version");
        var url = $"/api/v1/customers/{customer.Id()}";

        var first = await client.PutAsJsonAsync(url, new { type = "Company", name = "Acme Holding", primaryEmail = "info@acme.sa", preferredLanguage = "en", version = original }, Json);
        var updated = await first.ReadJson(HttpStatusCode.OK);
        updated.Str("name").ShouldBe("Acme Holding");
        updated.Str("version").ShouldNotBe(original);

        var second = await client.PutAsJsonAsync(url, new { type = "Company", name = "Acme Again", primaryEmail = "info@acme.sa", preferredLanguage = "en", version = original }, Json);
        (await second.ReadJson(HttpStatusCode.Conflict)).Str("code").ShouldBe("concurrency.stale");
    }

    [Fact]
    public async Task Contacts_can_be_added_updated_and_removed()
    {
        var client = await Factory.CreateClientAs(CrmApiFactory.SupportAgentEmail);
        var customer = await client.CreateCustomer();
        var url = $"/api/v1/customers/{customer.Id()}/contacts";

        var ali = await (await client.PostAsJsonAsync(url, new { name = "Ali", email = "ali@acme.sa", isPrimary = false }, Json)).ReadJson(HttpStatusCode.Created);
        var mona = await (await client.PostAsJsonAsync(url, new { name = "Mona", phone = "+966500000001", isPrimary = true }, Json)).ReadJson(HttpStatusCode.Created);

        var contacts = await client.GetFromJsonAsync<JsonElement>(url, Json);
        contacts.GetArrayLength().ShouldBe(2);
        contacts[0].Str("name").ShouldBe("Mona");
        contacts[0].GetProperty("isPrimary").GetBoolean().ShouldBeTrue();

        (await client.DeleteAsync($"{url}/{mona.Id()}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var remaining = await client.GetFromJsonAsync<JsonElement>(url, Json);
        remaining.GetArrayLength().ShouldBe(1);
        remaining[0].Id().ShouldBe(ali.Id());
        remaining[0].GetProperty("isPrimary").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Individuals_cannot_have_contacts()
    {
        var client = await Factory.CreateClientAs(CrmApiFactory.SupportAgentEmail);
        var person = await client.CreateCustomer("Sara Ali", "sara@mail.sa", type: "Individual");

        var response = await client.PostAsJsonAsync($"/api/v1/customers/{person.Id()}/contacts", new { name = "Ali" }, Json);

        (await response.ReadJson(HttpStatusCode.BadRequest)).Str("code").ShouldBe("customer.contacts-company-only");
    }

    [Fact]
    public async Task Timeline_shows_notes_and_tickets_newest_first()
    {
        var client = await Factory.CreateClientAs(CrmApiFactory.SupportAgentEmail);
        var customer = await client.CreateCustomer();
        await client.CreateTicket(Factory, customer.Id(), "Delivery delayed");
        var note = await client.PostAsJsonAsync($"/api/v1/customers/{customer.Id()}/notes", new { body = "Prefers WhatsApp contact" }, Json);
        (await note.ReadJson(HttpStatusCode.Created)).GetProperty("author").Str("fullName").ShouldBe("Omar Al-Harbi");

        var timeline = await client.GetFromJsonAsync<JsonElement>($"/api/v1/customers/{customer.Id()}/timeline", Json);

        var kinds = timeline.GetProperty("items").EnumerateArray().Select(i => i.Str("kind")).ToList();
        kinds.ShouldBe(["Note", "Ticket"]);
        timeline.GetProperty("totalCount").GetInt32().ShouldBe(2);
    }

    [Fact]
    public async Task Agents_cannot_delete_customers_and_the_attempt_is_audited()
    {
        var agent = await Factory.CreateClientAs(CrmApiFactory.SupportAgentEmail);
        var customer = await agent.CreateCustomer();

        var response = await agent.DeleteAsync($"/api/v1/customers/{customer.Id()}");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await Factory.WithDb(db => db.AuditLog.AnyAsync(a => a.Action == "PermissionDenied"))).ShouldBeTrue();
    }

    [Fact]
    public async Task Deactivated_customers_disappear_from_search_and_changes_are_audited()
    {
        var admin = await Factory.CreateClientAs(CrmApiFactory.AdminEmail);
        var customer = await admin.CreateCustomer();

        (await admin.DeleteAsync($"/api/v1/customers/{customer.Id()}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var page = await admin.GetFromJsonAsync<JsonElement>("/api/v1/customers", Json);
        page.GetProperty("totalCount").GetInt32().ShouldBe(0);

        var actions = await Factory.WithDb(db => db.AuditLog.Where(a => a.EntityType == "Customer").Select(a => a.Action).ToListAsync());
        actions.ShouldBe(["Create", "Delete"], ignoreOrder: true);
    }
}
