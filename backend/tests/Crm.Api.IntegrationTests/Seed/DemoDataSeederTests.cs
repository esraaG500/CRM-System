using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Domain.Common;
using Crm.Domain.Tickets;
using Crm.Infrastructure.Persistence;
using Crm.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Api.IntegrationTests.Seed;

public class DemoDataSeederTests(CrmApiFactory factory) : IntegrationTestBase(factory)
{
    private async Task Seed()
    {
        using var scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().SeedAsync();
    }

    [Fact]
    public async Task Seeds_consistent_tickets_and_is_idempotent()
    {
        await Seed();
        await Seed();

        await Factory.WithDb(async db =>
        {
            (await db.Customers.CountAsync(c => c.ErpReference!.StartsWith(DemoDataSeeder.Marker))).ShouldBe(24);
            var tickets = await db.Tickets.AsNoTracking().ToListAsync();
            tickets.Count.ShouldBe(96);

            // Every status the desk uses is represented.
            tickets.Select(t => t.Status).Distinct().Count().ShouldBe(Enum.GetValues<TicketStatus>().Length);

            // Domain invariants hold for generated data.
            tickets.Where(t => t.Status == TicketStatus.New).ShouldAllBe(t => t.AssigneeId == null);
            tickets.Where(t => t.Status == TicketStatus.Closed).ShouldAllBe(t => t.ClosedAt != null && t.ResolvedAt != null);
            tickets.Where(t => t.Status == TicketStatus.Resolved).ShouldAllBe(t => t.ResolvedAt != null && t.ClosedAt == null);
            tickets.ShouldAllBe(t => t.ReferenceNumber.StartsWith("TCK-"));
            tickets.ShouldContain(t => t.CreatedAt >= DateTimeOffset.UtcNow.Date);

            // Nothing is dated in the future.
            var now = DateTimeOffset.UtcNow;
            tickets.ShouldAllBe(t => t.CreatedAt <= now && (t.UpdatedAt == null || t.UpdatedAt <= now));
            (await db.TicketHistory.AnyAsync(h => h.Timestamp > now)).ShouldBeFalse();
            (await db.Messages.AnyAsync(m => m.CreatedAt > now)).ShouldBeFalse();

            var firstHistory = await db.TicketHistory.GroupBy(h => h.TicketId)
                .Select(g => g.OrderBy(h => h.Timestamp).ThenBy(h => h.Id).First().ChangeType).ToListAsync();
            firstHistory.ShouldAllBe(c => c == TicketChangeType.Created);

            // Replies come from agents; internal notes never have a customer sender.
            (await db.Messages.CountAsync(m => m.IsInternal && m.SenderCustomerId != null)).ShouldBe(0);
            (await db.Messages.AnyAsync(m => m.Direction == MessageDirection.Inbound)).ShouldBeTrue();
            return true;
        });
    }

    [Fact]
    public async Task Remove_deletes_only_demo_records()
    {
        var agent = await Factory.CreateClientAs(CrmApiFactory.SupportAgentEmail);
        var mine = await agent.CreateCustomer("Real Customer", "real@customer.sa");
        await agent.CreateTicket(Factory, mine.Id(), "Real ticket");
        await Seed();

        using (var scope = Factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().RemoveAsync();
        }

        await Factory.WithDb(async db =>
        {
            (await db.Customers.Select(c => c.Name).ToListAsync()).ShouldBe(["Real Customer"]);
            (await db.Tickets.Select(t => t.Subject).ToListAsync()).ShouldBe(["Real ticket"]);
            (await db.Messages.CountAsync()).ShouldBe(0);
            return true;
        });
    }

    [Fact]
    public async Task Demo_tickets_are_visible_through_the_api()
    {
        await Seed();
        var supervisor = await Factory.CreateClientAs(CrmApiFactory.SupervisorEmail);

        var page = await System.Net.Http.Json.HttpClientJsonExtensions.GetFromJsonAsync<System.Text.Json.JsonElement>(
            supervisor, "/api/v1/tickets?pageSize=100", Json);

        page.GetProperty("totalCount").GetInt32().ShouldBe(96);
    }
}
