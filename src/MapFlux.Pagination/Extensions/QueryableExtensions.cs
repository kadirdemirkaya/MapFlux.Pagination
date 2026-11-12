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

        return await ReadCursorPageAsync(
            source,
            opts,
            query => query,
            item => item,
            item => QueryableHelper.BuildCursor(item, opts),
            ct).ConfigureAwait(false);
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

    /// <summary>
    /// Applies the full offset pagination pipeline to <paramref name="source"/> and reads the requested
    /// page projected to <typeparamref name="TDest"/> by the query provider, so the page query selects only
    /// the columns the registered map reads instead of loading whole <typeparamref name="TSource"/> rows.
    /// Filters, search and sorting still name <typeparamref name="TSource"/> properties, the total is
    /// counted on the unprojected query, and the page is read in a second command, exactly like
    /// <see cref="ToPagedAsync{TSource, TDest}(IQueryable{TSource}, IMapper, PaginationOptions, CancellationToken)"/>.
    /// </summary>
    /// <remarks>
    /// A projection is evaluated by the query provider, not by the in-memory map: <see langword="null"/>
    /// handling and string comparison follow the provider, nested maps are chosen from declared member
    /// types, and member initializers of <typeparamref name="TDest"/> do not run for mapped members.
    /// See <see cref="IProjectionMapper"/>.
    /// </remarks>
    /// <param name="source">The query to page.</param>
    /// <param name="mapper">The mapper holding the registered map from <typeparamref name="TSource"/> to <typeparamref name="TDest"/>.</param>
    /// <param name="opts">The pagination options the page is read with.</param>
    /// <param name="ct">A token to cancel the query.</param>
    /// <returns>The requested, projected page.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/>, <paramref name="mapper"/> or <paramref name="opts"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// No map is registered for the type pair, or the map cannot be expressed as a projection. Raised before
    /// any command is sent; there is no fallback to mapping in memory.
    /// </exception>
    public static async Task<IPagedResult<TDest>> ToProjectedPagedAsync<TSource, TDest>(
        this IQueryable<TSource> source,
        IProjectionMapper mapper,
        PaginationOptions opts,
        CancellationToken ct = default)
    {
        if (source is null)
            throw new ArgumentNullException(nameof(source));
        if (mapper is null)
            throw new ArgumentNullException(nameof(mapper));
        if (opts is null)
            throw new ArgumentNullException(nameof(opts));

        var projection = mapper.GetProjection<TSource, TDest>();
        var query = QueryableHelper.ApplyFullPipeline(source, opts);

        var total = await CountAsyncOrSync(query, ct).ConfigureAwait(false);
        var items = await ToListAsyncOrSync(query.Skip(opts.Skip).Take(opts.Take).Select(projection), ct).ConfigureAwait(false);

        return new PagedResult<TDest>(items, total, opts.PageNumber, opts.PageSize);
    }

    /// <summary>
    /// Reads one keyset (cursor) page from <paramref name="source"/> projected to <typeparamref name="TDest"/>
    /// by the query provider. Ordering, the keyset filter and the cursors use
    /// <see cref="CursorPaginationOptions.CursorProperty"/> and <see cref="CursorPaginationOptions.SortBy"/> on
    /// <typeparamref name="TSource"/>, so <typeparamref name="TDest"/> does not need to carry them, and the
    /// cursors are the same ones the unprojected cursor page issues and accepts.
    /// </summary>
    /// <remarks>
    /// A projection is evaluated by the query provider, not by the in-memory map: <see langword="null"/>
    /// handling and string comparison follow the provider, nested maps are chosen from declared member
    /// types, and member initializers of <typeparamref name="TDest"/> do not run for mapped members.
    /// See <see cref="IProjectionMapper"/>.
    /// </remarks>
    /// <param name="source">The query to page.</param>
    /// <param name="mapper">The mapper holding the registered map from <typeparamref name="TSource"/> to <typeparamref name="TDest"/>.</param>
    /// <param name="opts">The cursor pagination options the page is read with.</param>
    /// <param name="ct">A token to cancel the query.</param>
    /// <returns>The requested, projected cursor page.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/>, <paramref name="mapper"/> or <paramref name="opts"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="CursorPaginationOptions.PageSize"/> is less than 1.</exception>
    /// <exception cref="InvalidOperationException">
    /// No map is registered for the type pair, or the map cannot be expressed as a projection. Raised before
    /// any command is sent; there is no fallback to mapping in memory.
    /// </exception>
    public static async Task<ICursorPagedResult<TDest>> ToProjectedCursorPagedAsync<TSource, TDest>(
        this IQueryable<TSource> source,
        IProjectionMapper mapper,
        CursorPaginationOptions opts,
        CancellationToken ct = default)
    {
        if (source is null)
            throw new ArgumentNullException(nameof(source));
        if (mapper is null)
            throw new ArgumentNullException(nameof(mapper));
        if (opts is null)
            throw new ArgumentNullException(nameof(opts));
        if (opts.PageSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(opts), opts.PageSize, "PageSize must be greater than or equal to 1.");

        var rowProjection = QueryableHelper.BuildCursorRowProjection(mapper.GetProjection<TSource, TDest>(), opts);

        return await ReadCursorPageAsync(
            source,
            opts,
            query => query.Select(rowProjection),
            row => row.Item,
            QueryableHelper.BuildRowCursor,
            ct).ConfigureAwait(false);
    }

    private static async Task<CursorPagedResult<TItem>> ReadCursorPageAsync<TSource, TRow, TItem>(
        IQueryable<TSource> source,
        CursorPaginationOptions opts,
        Func<IQueryable<TSource>, IQueryable<TRow>> select,
        Func<TRow, TItem> toItem,
        Func<TRow, string?> toCursor,
        CancellationToken ct)
    {
        var total = opts.IncludeTotalCount
            ? await CountAsyncOrSync(source, ct).ConfigureAwait(false)
            : -1;
        var plan = QueryableHelper.BuildCursorQuery(source, opts);

        var rows = await ToListAsyncOrSync(select(plan.Query.Take(opts.PageSize + 1)), ct).ConfigureAwait(false);
        var hasMoreBeyondPage = rows.Count > opts.PageSize;

        if (hasMoreBeyondPage)
            rows = rows.Take(opts.PageSize).ToList();

        if (plan.Reversed)
            rows.Reverse();

        string? startCursor = null;
        string? endCursor = null;

        if (rows.Count > 0)
        {
            startCursor = toCursor(rows[0]);
            endCursor = toCursor(rows[^1]);
        }

        var hasNextPage = plan.Reversed || hasMoreBeyondPage;
        var hasPreviousPage = plan.Reversed ? hasMoreBeyondPage : plan.CursorApplied;
        var items = rows.Select(toItem).ToList();

        return new CursorPagedResult<TItem>(items, startCursor, endCursor, hasNextPage, hasPreviousPage, total);
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
