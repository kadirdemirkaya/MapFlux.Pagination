using MapFlux.Pagination.Models;

namespace MapFlux.Pagination.Abstractions;

public interface IPaginatedMapper<TSource, TDest>
{
    Task<IPagedResult<TDest>> MapPagedAsync(
        IQueryable<TSource> source, 
        PaginationOptions opts, 
        CancellationToken ct = default);

    IPagedResult<TDest> MapPaged(IPagedResult<TSource> source);
}
