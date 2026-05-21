using MapFlux.Pagination.Models;

namespace MapFlux.Pagination.Abstractions;

public interface IPaginatedMapper<TSource, TDest>
{
    Task<IPagedResult<TDest>> MapPagedAsync(
        IQueryable<TSource> source, 
        PaginationOptions opts, 
        CancellationToken ct = default);

    IPagedResult<TDest> MapPaged(IPagedResult<TSource> source);

    /// <summary>
    /// Cursor-based pagination with mapping.
    /// </summary>
    Task<ICursorPagedResult<TDest>> MapCursorPagedAsync(
        IQueryable<TSource> source,
        CursorPaginationOptions opts,
        CancellationToken ct = default);
}
