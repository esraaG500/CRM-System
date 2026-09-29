using Crm.Application.Abstractions;
using Crm.Application.Common.Messaging;
using Crm.Application.Common.Paging;
using Crm.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Crm.Application.Customers;

public sealed record SearchCustomersQuery(
    string? Q,
    CustomerType? Type,
    Guid? BranchId,
    bool? NeedsReview,
    PageRequest Page) : IRequest<PagedResult<CustomerSummaryDto>>;

internal sealed class SearchCustomersHandler(IAppDbContext db) : IRequestHandler<SearchCustomersQuery, PagedResult<CustomerSummaryDto>>
{
    private static readonly TicketStatus[] ClosedStatuses = [TicketStatus.Resolved, TicketStatus.Closed];

    public Task<PagedResult<CustomerSummaryDto>> Handle(SearchCustomersQuery request, CancellationToken ct)
    {
        var query = db.Customers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Q))
        {
            var term = request.Q.Trim();
            var like = $"%{EscapeLike(term)}%";
            var reference = term.ToUpperInvariant();
            var phone = term.Replace(" ", string.Empty, StringComparison.Ordinal);
            query = query.Where(c =>
                c.ReferenceNumber == reference
                || EF.Functions.Like(c.Name, like)
                || (c.PrimaryEmail != null && EF.Functions.Like(c.PrimaryEmail, like))
                || (c.PrimaryPhone != null && c.PrimaryPhone.Contains(phone))
                || c.Contacts.Any(p => EF.Functions.Like(p.Name, like) || (p.Email != null && EF.Functions.Like(p.Email, like))));
        }

        if (request.Type is not null)
        {
            query = query.Where(c => c.Type == request.Type);
        }

        if (request.BranchId is not null)
        {
            query = query.Where(c => c.BranchId == request.BranchId);
        }

        if (request.NeedsReview is not null)
        {
            query = query.Where(c => c.NeedsReview == request.NeedsReview);
        }

        return query
            .OrderBy(c => c.Name)
            .Select(c => new CustomerSummaryDto(
                c.Id,
                c.ReferenceNumber,
                c.Type,
                c.Name,
                c.PrimaryEmail,
                c.PrimaryPhone,
                db.Tickets.Count(t => t.CustomerId == c.Id && !ClosedStatuses.Contains(t.Status)),
                c.NeedsReview))
            .ToPagedResultAsync(request.Page, ct);
    }

    private static string EscapeLike(string value) =>
        value.Replace("[", "[[]", StringComparison.Ordinal)
             .Replace("%", "[%]", StringComparison.Ordinal)
             .Replace("_", "[_]", StringComparison.Ordinal);
}
