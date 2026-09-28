using MapFlux.Pagination.Core;
using MapFlux.Pagination.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using MapFlux;

namespace MapFlux.Pagination.Extensions;

/// <summary>EF Core-backed offset and cursor pagination over an <see cref="IQueryable{T}"/>.</summary>
public static class QueryableExtensions
{
    /// <summary>
    /// Applies the full offset pagination pipeline (filter, search, sort) to <paramref name="source"/>
    /// and reads the requested page, awaiting asynchronously when the provider supports it (e.g. EF Core)
    /// and synchronously otherwise.
    /// </summary>
    /// <param name="source">The query to page.</param>
    /// <param name="opts">The pagination options the page is read with.</param>
    /// <param name="ct">A token to cancel the query.</param>
    /// <returns>The requested page.</returns>
    public static async Task<IPagedResult<T>> ToPagedAsync<T>(
        this IQueryable<T> source,
        PaginationOptions opts,
        CancellationToken ct = default)
    {
        var query = QueryableHelper.ApplyFullPipeline(source, opts);

        var total = await CountAsyncOrSync(query, ct).ConfigureAwait(false);
        var items = await ToListAsyncOrSync(query.Skip(opts.Skip).Take(opts.Take), ct).ConfigureAwait(false);

        return new PagedResult<T>(items, total, opts.PageNumber, opts.PageSize);
    }

    /// <summary>
    /// Applies the full offset pagination pipeline to <paramref name="source"/>, reads the requested
    /// page and maps each item to <typeparamref name="TDest"/>.
    /// </summary>
    /// <param name="source">The query to page.</param>
    /// <param name="mapper">The mapper used to project each page item.</param>
    /// <param name="opts">The pagination options the page is read with.</param>
    /// <param name="ct">A token to cancel the query.</param>
    /// <returns>The requested, mapped page.</returns>
    public static async Task<IPagedResult<TDest>> ToPagedAsync<TSource, TDest>(
        this IQueryable<TSource> source,
        IMapper mapper,
        PaginationOptions opts,
        CancellationToken ct = default)
    {
        var paged = await source.ToPagedAsync(opts, ct).ConfigureAwait(false);
        var mapped = paged.Items.Select(e => mapper.Map<TSource, TDest>(e)).ToList();

        return new PagedResult<TDest>(mapped, paged.TotalCount, paged.PageNumber, paged.PageSize);
    }

    /// <summary>
    /// Reads one keyset (cursor) page from <paramref name="source"/> ordered by
    /// <see cref="CursorPaginationOptions.CursorProperty"/> (and <see cref="CursorPaginationOptions.SortBy"/>
    /// when set).
    /// </summary>
    /// <param name="source">The query to page.</param>
    /// <param name="opts">The cursor pagination options the page is read with.</param>
    /// <param name="ct">A token to cancel the query.</param>
    /// <returns>The requested cursor page.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="CursorPaginationOptions.PageSize"/> is less than 1.</exception>
    public static async Task<ICursorPagedResult<T>> ToCursorPagedAsync<T>(
        this IQueryable<T> source,
        CursorPaginationOptions opts,
        CancellationToken ct = default)
    {
        if (opts.PageSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(opts), opts.PageSize, "PageSize must be greater than or equal to 1.");

        var total = opts.IncludeTotalCount
            ? await CountAsyncOrSync(source, ct).ConfigureAwait(false)
            : -1;
        var plan = QueryableHelper.BuildCursorQuery(source, opts);

        var items = await ToListAsyncOrSync(plan.Query.Take(opts.PageSize + 1), ct).ConfigureAwait(false);
        var hasMoreBeyondPage = items.Count > opts.PageSize;

        if (hasMoreBeyondPage)
            items = items.Take(opts.PageSize).ToList();

        if (plan.Reversed)
            items.Reverse();

        string? startCursor = null;
        string? endCursor = null;

        if (items.Count > 0)
        {
            startCursor = QueryableHelper.BuildCursor(items[0], opts);
            endCursor = QueryableHelper.BuildCursor(items[^1], opts);
        }

        var hasNextPage = plan.Reversed || hasMoreBeyondPage;
        var hasPreviousPage = plan.Reversed ? hasMoreBeyondPage : plan.CursorApplied;

        return new CursorPagedResult<T>(items, startCursor, endCursor, hasNextPage, hasPreviousPage, total);
    }

    /// <summary>
    /// Reads one keyset (cursor) page from <paramref name="source"/> and maps each item to
    /// <typeparamref name="TDest"/>.
    /// </summary>
    /// <param name="source">The query to page.</param>
    /// <param name="mapper">The mapper used to project each page item.</param>
    /// <param name="opts">The cursor pagination options the page is read with.</param>
    /// <param name="ct">A token to cancel the query.</param>
    /// <returns>The requested, mapped cursor page.</returns>
    public static async Task<ICursorPagedResult<TDest>> ToCursorPagedAsync<TSource, TDest>(
        this IQueryable<TSource> source,
        IMapper mapper,
        CursorPaginationOptions opts,
        CancellationToken ct = default)
    {
        var paged = await source.ToCursorPagedAsync(opts, ct).ConfigureAwait(false);
        var mapped = paged.Items.Select(e => mapper.Map<TSource, TDest>(e)).ToList();

        return new CursorPagedResult<TDest>(
            mapped, paged.StartCursor, paged.EndCursor,
            paged.HasNextPage, paged.HasPreviousPage, paged.TotalCount);
    }

    private static Task<int> CountAsyncOrSync<T>(IQueryable<T> source, CancellationToken ct)
        => source.Provider is IAsyncQueryProvider
            ? source.CountAsync(ct)
            : Task.FromResult(source.Count());

    private static Task<List<T>> ToListAsyncOrSync<T>(IQueryable<T> source, CancellationToken ct)
        => source.Provider is IAsyncQueryProvider
            ? source.ToListAsync(ct)
            : Task.FromResult(source.ToList());
}
