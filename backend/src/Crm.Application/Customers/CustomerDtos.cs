using Crm.Domain.Common;
using Crm.Domain.Customers;

namespace Crm.Application.Customers;

public sealed record AddressDto(string? Line1, string? City, string? Country);

public sealed record CustomerUpsertDto(
    CustomerType Type,
    string Name,
    string? PrimaryEmail,
    string? PrimaryPhone,
    AddressDto? Address,
    Language PreferredLanguage,
    Guid? BranchId,
    string? ErpReference,
    string? Version);

public sealed record ContactPersonDto(Guid Id, string Name, string? Email, string? Phone, string? JobTitle, bool IsPrimary);

public sealed record ContactPersonUpsertDto(string Name, string? Email, string? Phone, string? JobTitle, bool IsPrimary);

public sealed record CustomerDto(
    Guid Id,
    string ReferenceNumber,
    CustomerType Type,
    string Name,
    string? PrimaryEmail,
    string? PrimaryPhone,
    AddressDto? Address,
    Language PreferredLanguage,
    Guid? BranchId,
    string? ErpReference,
    bool NeedsReview,
    IReadOnlyList<ContactPersonDto> Contacts,
    DateTimeOffset CreatedAt,
    string Version);

public sealed record CustomerSummaryDto(
    Guid Id,
    string ReferenceNumber,
    CustomerType Type,
    string Name,
    string? PrimaryEmail,
    string? PrimaryPhone,
    int OpenTicketCount,
    bool NeedsReview);

public sealed record NoteDto(Guid Id, string Body, UserRefDto Author, DateTimeOffset CreatedAt);

public sealed record UserRefDto(Guid Id, string FullName);

public enum TimelineKind { Ticket, Message, Note }

public sealed record TimelineItemDto(
    TimelineKind Kind,
    DateTimeOffset OccurredAt,
    string Title,
    string Excerpt,
    Channel? Channel,
    Guid? TicketId,
    UserRefDto? Actor);

internal static class CustomerMapping
{
    public static CustomerDto ToDto(this Customer c) => new(
        c.Id,
        c.ReferenceNumber,
        c.Type,
        c.Name,
        c.PrimaryEmail,
        c.PrimaryPhone,
        c.Address is null ? null : new AddressDto(c.Address.Line1, c.Address.City, c.Address.Country),
        c.PreferredLanguage,
        c.BranchId,
        c.ErpReference,
        c.NeedsReview,
        c.Contacts.OrderByDescending(x => x.IsPrimary).ThenBy(x => x.Name).Select(ToDto).ToList(),
        c.CreatedAt,
        Common.Versioning.ToVersion(c.RowVersion));

    public static ContactPersonDto ToDto(this ContactPerson p) => new(p.Id, p.Name, p.Email, p.Phone, p.JobTitle, p.IsPrimary);

    public static Address? ToAddress(this AddressDto? a) =>
        a is null || (string.IsNullOrWhiteSpace(a.Line1) && string.IsNullOrWhiteSpace(a.City) && string.IsNullOrWhiteSpace(a.Country))
            ? null
            : new Address(a.Line1?.Trim(), a.City?.Trim(), a.Country?.Trim().ToUpperInvariant());
}
