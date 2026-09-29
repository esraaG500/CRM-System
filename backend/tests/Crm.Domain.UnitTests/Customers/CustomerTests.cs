using Crm.Domain.Common;
using Crm.Domain.Customers;

namespace Crm.Domain.UnitTests.Customers;

public class CustomerTests
{
    private static Customer NewCompany() =>
        new(CustomerType.Company, "Acme", "info@acme.sa", null, Language.Ar, branchId: null);

    [Fact]
    public void Requires_email_or_phone()
    {
        var ex = Should.Throw<DomainException>(() =>
            new Customer(CustomerType.Individual, "Sara", null, "  ", Language.Ar, null));

        ex.Code.ShouldBe("customer.contact-required");
    }

    [Theory]
    [InlineData("0501234567")]
    [InlineData("+0501234567")]
    [InlineData("+96650")]
    public void Rejects_phone_not_in_E164(string phone)
    {
        var ex = Should.Throw<DomainException>(() =>
            new Customer(CustomerType.Individual, "Sara", null, phone, Language.Ar, null));

        ex.Code.ShouldBe("customer.phone-format");
    }

    [Fact]
    public void Normalizes_email_and_trims_name()
    {
        var customer = new Customer(CustomerType.Individual, "  Sara Ali ", " Sara@Example.COM ", "+966501234567", Language.En, null);

        customer.Name.ShouldBe("Sara Ali");
        customer.PrimaryEmail.ShouldBe("sara@example.com");
        customer.PrimaryPhone.ShouldBe("+966501234567");
    }

    [Fact]
    public void Only_companies_can_have_contacts()
    {
        var person = new Customer(CustomerType.Individual, "Sara", "s@x.sa", null, Language.Ar, null);

        Should.Throw<DomainException>(() => person.AddContact("Ali", null, null, null, false))
            .Code.ShouldBe("customer.contacts-company-only");
    }

    [Fact]
    public void First_contact_becomes_primary_and_primary_moves_on_removal()
    {
        var company = NewCompany();
        var first = company.AddContact("Ali", "ali@acme.sa", null, "IT", isPrimary: false);
        var second = company.AddContact("Mona", null, "+966500000001", null, isPrimary: false);

        first.IsPrimary.ShouldBeTrue();
        second.IsPrimary.ShouldBeFalse();

        company.UpdateContact(second.Id, "Mona", null, "+966500000001", "Manager", isPrimary: true);
        first.IsPrimary.ShouldBeFalse();
        second.IsPrimary.ShouldBeTrue();

        company.RemoveContact(second.Id);
        first.IsPrimary.ShouldBeTrue();
        company.Contacts.Count.ShouldBe(1);
    }

    [Fact]
    public void Cannot_change_company_with_contacts_to_individual()
    {
        var company = NewCompany();
        company.AddContact("Ali", null, null, null, true);

        Should.Throw<DomainException>(() => company.UpdateProfile(CustomerType.Individual, Language.Ar, null, null, null))
            .Code.ShouldBe("customer.type-has-contacts");
    }

    [Fact]
    public void Unknown_sender_is_flagged_for_review()
    {
        var customer = Customer.CreateFromUnknownSender(null, "new@client.sa", null, Language.Ar);

        customer.NeedsReview.ShouldBeTrue();
        customer.Name.ShouldBe("new@client.sa");

        customer.MarkReviewed();
        customer.NeedsReview.ShouldBeFalse();
    }
}
