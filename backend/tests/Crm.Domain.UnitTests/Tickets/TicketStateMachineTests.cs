using Crm.Domain.Common;
using Crm.Domain.Tickets;

namespace Crm.Domain.UnitTests.Tickets;

public class TicketStateMachineTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 29, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Agent = Guid.NewGuid();

    private static Ticket NewTicket() => Ticket.Create(
        "Printer not working", "The office printer shows error 42.", Guid.NewGuid(), null, Channel.Phone,
        Guid.NewGuid(), TicketPriority.High, Guid.NewGuid(), null, Agent, Now);

    [Fact]
    public void New_ticket_starts_New_with_created_history()
    {
        var ticket = NewTicket();

        ticket.Status.ShouldBe(TicketStatus.New);
        ticket.History.Single().ChangeType.ShouldBe(TicketChangeType.Created);
    }

    [Fact]
    public void Assigning_a_new_ticket_opens_it_and_records_both_changes()
    {
        var ticket = NewTicket();

        ticket.Assign(Agent, Agent, Now);

        ticket.AssigneeId.ShouldBe(Agent);
        ticket.Status.ShouldBe(TicketStatus.Open);
        ticket.History.Select(h => h.ChangeType)
            .ShouldBe([TicketChangeType.Created, TicketChangeType.Assigned, TicketChangeType.StatusChanged]);
    }

    [Fact]
    public void Take_fails_when_already_assigned()
    {
        var ticket = NewTicket();
        ticket.Assign(Guid.NewGuid(), Agent, Now);

        Should.Throw<ConflictException>(() => ticket.Take(Agent, Now)).Code.ShouldBe("ticket.already-assigned");
    }

    [Theory]
    [InlineData(TicketStatus.New, TicketStatus.Resolved)]
    [InlineData(TicketStatus.New, TicketStatus.PendingCustomer)]
    public void Rejects_invalid_transitions(TicketStatus from, TicketStatus to)
    {
        var ticket = NewTicket();
        ticket.Status.ShouldBe(from);

        Should.Throw<ConflictException>(() => ticket.ChangeStatus(to, Agent, Now))
            .Code.ShouldBe("ticket.invalid-transition");
    }

    [Fact]
    public void Full_lifecycle_sets_timestamps()
    {
        var ticket = NewTicket();
        ticket.Assign(Agent, Agent, Now);
        ticket.ChangeStatus(TicketStatus.InProgress, Agent, Now.AddMinutes(5));
        ticket.ChangeStatus(TicketStatus.Resolved, Agent, Now.AddHours(1));
        ticket.ChangeStatus(TicketStatus.Closed, Agent, Now.AddHours(2));

        ticket.Status.ShouldBe(TicketStatus.Closed);
        ticket.ResolvedAt.ShouldBe(Now.AddHours(1));
        ticket.ClosedAt.ShouldBe(Now.AddHours(2));
        ticket.AllowedTransitions().ShouldBe([TicketStatus.Open]);
    }

    [Fact]
    public void Closing_an_unresolved_ticket_requires_reason()
    {
        var ticket = NewTicket();

        Should.Throw<DomainException>(() => ticket.ChangeStatus(TicketStatus.Closed, Agent, Now))
            .Code.ShouldBe("ticket.close-reason-required");

        ticket.ChangeStatus(TicketStatus.Closed, Agent, Now, "Duplicate of TCK-000001");
        ticket.History[^1].Reason.ShouldBe("Duplicate of TCK-000001");
    }

    [Fact]
    public void Closed_ticket_cannot_be_edited()
    {
        var ticket = NewTicket();
        ticket.ChangeStatus(TicketStatus.Closed, Agent, Now, "Spam");

        Should.Throw<ConflictException>(() => ticket.Assign(Agent, Agent, Now)).Code.ShouldBe("ticket.closed");
    }

    [Fact]
    public void Customer_reply_within_7_days_reopens_closed_ticket()
    {
        var ticket = NewTicket();
        ticket.ChangeStatus(TicketStatus.Closed, Agent, Now, "Answered by phone");

        var reopened = ticket.RecordCustomerReply(Now.AddDays(6), TimeSpan.FromDays(7));

        reopened.ShouldBeTrue();
        ticket.Status.ShouldBe(TicketStatus.Open);
        ticket.ClosedAt.ShouldBeNull();
        ticket.History[^1].ChangeType.ShouldBe(TicketChangeType.Reopened);
    }

    [Fact]
    public void Customer_reply_after_7_days_does_not_reopen()
    {
        var ticket = NewTicket();
        ticket.ChangeStatus(TicketStatus.Closed, Agent, Now, "Answered by phone");

        ticket.RecordCustomerReply(Now.AddDays(8), TimeSpan.FromDays(7)).ShouldBeFalse();
        ticket.Status.ShouldBe(TicketStatus.Closed);
    }

    [Fact]
    public void Escalation_increments_level_reassigns_and_caps_at_three()
    {
        var ticket = NewTicket();
        var owner = Guid.NewGuid();

        ticket.Escalate("Customer is a VIP", owner, Agent, Now);

        ticket.EscalationLevel.ShouldBe(1);
        ticket.AssigneeId.ShouldBe(owner);

        ticket.Escalate("Still waiting", null, Agent, Now);
        ticket.Escalate("Management", null, Agent, Now);
        Should.Throw<ConflictException>(() => ticket.Escalate("Again", null, Agent, Now))
            .Code.ShouldBe("ticket.max-escalation");
    }

    [Fact]
    public void Escalation_requires_reason()
    {
        Should.Throw<DomainException>(() => NewTicket().Escalate(" ", null, Agent, Now))
            .Code.ShouldBe("ticket.escalation-reason-required");
    }

    [Fact]
    public void First_agent_reply_sets_first_response_once()
    {
        var ticket = NewTicket();

        ticket.RecordAgentReply(Agent, Now.AddMinutes(10));
        ticket.RecordAgentReply(Agent, Now.AddMinutes(20));

        ticket.FirstRespondedAt.ShouldBe(Now.AddMinutes(10));
        ticket.Status.ShouldBe(TicketStatus.Open);
    }

    [Fact]
    public void Update_details_records_only_changed_fields()
    {
        var ticket = NewTicket();
        var newCategory = Guid.NewGuid();

        ticket.UpdateDetails(ticket.Subject, newCategory, TicketPriority.Urgent, ticket.DepartmentId, Agent, Now);

        var changes = ticket.History.Where(h => h.ChangeType == TicketChangeType.FieldChanged).Select(h => h.Field);
        changes.ShouldBe(["CategoryId", "Priority"]);
    }
}
