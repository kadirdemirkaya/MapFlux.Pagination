using MapFlux.Pagination.Models;

namespace MapFlux.Pagination.Abstractions;

/// <summary>
/// Pages an <see cref="IQueryable{T}"/> or an already paged result of <typeparamref name="TSource"/>
/// and maps the page to <typeparamref name="TDest"/>.
/// </summary>
/// <typeparam name="TSource">The source entity type read from the query.</typeparam>
/// <typeparam name="TDest">The destination type each item is mapped to.</typeparam>
public interface IPaginatedMapper<TSource, TDest>
{
    /// <summary>
    /// Applies the offset pagination pipeline to <paramref name="source"/> and maps the resulting page.
    /// </summary>
    /// <param name="source">The query to page.</param>
    /// <param name="opts">The pagination options the page is read with.</param>
    /// <param name="ct">A token to cancel the query.</param>
    /// <returns>The mapped page.</returns>
    Task<IPagedResult<TDest>> MapPagedAsync(
        IQueryable<TSource> source,
        PaginationOptions opts,
        CancellationToken ct = default);

    /// <summary>
    /// Maps an already paged result of <typeparamref name="TSource"/> to <typeparamref name="TDest"/>.
    /// </summary>
    /// <param name="source">The paged result to map.</param>
    /// <returns>The mapped page.</returns>
    IPagedResult<TDest> MapPaged(IPagedResult<TSource> source);

    /// <summary>
    /// Cursor-based pagination with mapping.
    /// </summary>
    /// <param name="source">The query to page.</param>
    /// <param name="opts">The cursor pagination options the page is read with.</param>
    /// <param name="ct">A token to cancel the query.</param>
    /// <returns>The mapped cursor page.</returns>
    Task<ICursorPagedResult<TDest>> MapCursorPagedAsync(
        IQueryable<TSource> source,
        CursorPaginationOptions opts,
        CancellationToken ct = default);
}
