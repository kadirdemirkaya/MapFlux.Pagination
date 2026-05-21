using MapFlux.Pagination.Core;
using MapFlux.Pagination.Models;
using Microsoft.EntityFrameworkCore;
using MapFlux;

namespace MapFlux.Pagination.Extensions;

public static class QueryableExtensions
{
    public static async Task<IPagedResult<T>> ToPagedAsync<T>(
        this IQueryable<T> source,
        PaginationOptions opts,
        CancellationToken ct = default)
    {
        var query = QueryableHelper.ApplyFullPipeline(source, opts);

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

    public static async Task<ICursorPagedResult<T>> ToCursorPagedAsync<T>(
        this IQueryable<T> source,
        CursorPaginationOptions opts,
        CancellationToken ct = default)
    {
        var total = await source.CountAsync(ct);
        var query = QueryableHelper.ApplyCursorFilter(source, opts);

        // Take PageSize + 1 to determine if there are more items
        var items = await query.Take(opts.PageSize + 1).ToListAsync(ct);
        var hasNextPage = items.Count > opts.PageSize;

        if (hasNextPage)
            items = items.Take(opts.PageSize).ToList();

        string? startCursor = null;
        string? endCursor = null;

        if (items.Count > 0)
        {
            var cursorProperty = typeof(T).GetProperty(opts.CursorProperty,
                System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            if (cursorProperty != null)
            {
                var firstValue = cursorProperty.GetValue(items[0]);
                var lastValue = cursorProperty.GetValue(items[^1]);

                if (firstValue != null)
                    startCursor = QueryableHelper.EncodeCursor(firstValue);
                if (lastValue != null)
                    endCursor = QueryableHelper.EncodeCursor(lastValue);
            }
        }

        var hasPreviousPage = !string.IsNullOrWhiteSpace(opts.After);

        return new CursorPagedResult<T>(items, startCursor, endCursor, hasNextPage, hasPreviousPage, total);
    }

    public static async Task<ICursorPagedResult<TDest>> ToCursorPagedAsync<TSource, TDest>(
        this IQueryable<TSource> source,
        IMapper mapper,
        CursorPaginationOptions opts,
        CancellationToken ct = default)
    {
        var paged = await source.ToCursorPagedAsync(opts, ct);
        var mapped = paged.Items.Select(e => mapper.Map<TSource, TDest>(e)).ToList();

        return new CursorPagedResult<TDest>(
            mapped, paged.StartCursor, paged.EndCursor,
            paged.HasNextPage, paged.HasPreviousPage, paged.TotalCount);
    }
}
