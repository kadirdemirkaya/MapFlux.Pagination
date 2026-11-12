using MapFlux.Pagination.Models;

namespace MapFlux.Pagination.Abstractions;

/// <summary>
/// Pages an <see cref="IQueryable{T}"/> of <typeparamref name="TSource"/> and lets the query provider project
/// each row to <typeparamref name="TDest"/>, so the page query selects only the columns the registered map
/// reads. Filters, search, sorting and cursors still name <typeparamref name="TSource"/> properties.
/// </summary>
/// <remarks>
/// A projection is evaluated by the query provider, not by the in-memory map: <see langword="null"/>
/// handling and string comparison follow the provider, nested maps are chosen from declared member types,
/// and member initializers of <typeparamref name="TDest"/> do not run for mapped members. A map that cannot
/// be expressed as a projection throws <see cref="InvalidOperationException"/>; there is no fallback to
/// mapping in memory. <see cref="IPaginatedMapper{TSource, TDest}"/> keeps mapping in memory.
/// </remarks>
/// <typeparam name="TSource">The source entity type read from the query.</typeparam>
/// <typeparam name="TDest">The destination type each row is projected to.</typeparam>
public interface IProjectedPaginatedMapper<TSource, TDest>
{
    /// <summary>
    /// Applies the offset pagination pipeline to <paramref name="source"/> and reads the page projected to
    /// <typeparamref name="TDest"/>. The total is counted on the unprojected query.
    /// </summary>
    /// <param name="source">The query to page.</param>
    /// <param name="opts">The pagination options the page is read with.</param>
    /// <param name="ct">A token to cancel the query.</param>
    /// <returns>The projected page.</returns>
    /// <exception cref="InvalidOperationException">
    /// The mapper cannot build projections, no map is registered for the type pair, or the map cannot be
    /// expressed as a projection.
    /// </exception>
    Task<IPagedResult<TDest>> ProjectPagedAsync(
        IQueryable<TSource> source,
        PaginationOptions opts,
        CancellationToken ct = default);

    /// <summary>
    /// Reads one keyset (cursor) page from <paramref name="source"/> projected to <typeparamref name="TDest"/>.
    /// <typeparamref name="TDest"/> does not need the cursor or sort property; the cursors are the same ones
    /// the unprojected cursor page issues and accepts.
    /// </summary>
    /// <param name="source">The query to page.</param>
    /// <param name="opts">The cursor pagination options the page is read with.</param>
    /// <param name="ct">A token to cancel the query.</param>
    /// <returns>The projected cursor page.</returns>
    /// <exception cref="InvalidOperationException">
    /// The mapper cannot build projections, no map is registered for the type pair, or the map cannot be
    /// expressed as a projection.
    /// </exception>
    Task<ICursorPagedResult<TDest>> ProjectCursorPagedAsync(
        IQueryable<TSource> source,
        CursorPaginationOptions opts,
        CancellationToken ct = default);
}
