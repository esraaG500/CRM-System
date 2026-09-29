using Microsoft.EntityFrameworkCore;

namespace Crm.Application.Common.Paging;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public sealed record PageRequest(int Page = 1, int PageSize = PageRequest.DefaultPageSize)
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;

    public int SafePage => Math.Max(1, Page);
    public int SafePageSize => Math.Clamp(PageSize, 1, MaxPageSize);
    public int Skip => (SafePage - 1) * SafePageSize;
}

public static class PagingExtensions
{
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(this IQueryable<T> query, PageRequest page, CancellationToken ct)
    {
        var total = await query.CountAsync(ct);
        var items = await query.Skip(page.Skip).Take(page.SafePageSize).ToListAsync(ct);
        return new PagedResult<T>(items, page.SafePage, page.SafePageSize, total);
    }
}
