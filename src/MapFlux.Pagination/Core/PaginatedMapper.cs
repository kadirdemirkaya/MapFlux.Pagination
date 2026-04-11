using MapFlux;
using MapFlux.Pagination.Abstractions;
using MapFlux.Pagination.Models;
using MapFlux.Pagination.Extensions;

namespace MapFlux.Pagination.Core;

public class PaginatedMapper<TSource, TDest> : IPaginatedMapper<TSource, TDest>
{
    private readonly IMapper _mapper;

    public PaginatedMapper(IMapper mapper)
    {
        _mapper = mapper;
    }

    public async Task<IPagedResult<TDest>> MapPagedAsync(
        IQueryable<TSource> source, 
        PaginationOptions opts, 
        CancellationToken ct = default)
    {
        var pagedSource = await source.ToPagedAsync(opts, ct);
        return MapPaged(pagedSource);
    }

    public IPagedResult<TDest> MapPaged(IPagedResult<TSource> source)
    {
        var mappedItems = _mapper.MapList<TSource, TDest>(source.Items);
        
        return new PagedResult<TDest>(mappedItems, source.TotalCount, source.PageNumber, source.PageSize);
    }
}
