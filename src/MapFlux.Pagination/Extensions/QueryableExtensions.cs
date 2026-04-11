using MapFlux.Pagination.Core;
using MapFlux.Pagination.Models;
using Microsoft.EntityFrameworkCore;

namespace MapFlux.Pagination.Extensions;

public static class QueryableExtensions
{
    public static async Task<IPagedResult<T>> ToPagedAsync<T>(
        this IQueryable<T> source,
        PaginationOptions opts,
        CancellationToken ct = default)
    {
        var query = QueryableHelper.ApplySorting(source, opts.SortBy, opts.SortDescending);

        var total = await query.CountAsync(ct);
        var items = await query.Skip(opts.Skip).Take(opts.Take).ToListAsync(ct);

        return new PagedResult<T>(items, total, opts.PageNumber, opts.PageSize);
    }

    public static async Task<IPagedResult<TDest>> ToPagedAsync<TSource, TDest>(
        this IQueryable<TSource> source,
        IMapper mapper,
        PaginationOptions opts,
        CancellationToken ct = default)
    {
        var paged = await source.ToPagedAsync(opts, ct);
        var mapped = paged.Items.Select(e => mapper.Map<TSource, TDest>(e)).ToList();

        return new PagedResult<TDest>(mapped, paged.TotalCount, paged.PageNumber, paged.PageSize);
    }
}
