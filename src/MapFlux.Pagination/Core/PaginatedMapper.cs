using MapFlux;
using MapFlux.Pagination.Abstractions;
using MapFlux.Pagination.Models;
using MapFlux.Pagination.Extensions;

namespace MapFlux.Pagination.Core;

public class PaginatedMapper<TSource, TDest> : IPaginatedMapper<TSource, TDest>
{
    private readonly IMapper _mapper;
    private readonly PaginationGlobalOptions? _globalOptions;

    public PaginatedMapper(IMapper mapper)
        : this(mapper, null)
    {
    }

    public PaginatedMapper(IMapper mapper, PaginationGlobalOptions? globalOptions)
    {
        _mapper = mapper;
        _globalOptions = globalOptions;
    }

    public async Task<IPagedResult<TDest>> MapPagedAsync(
        IQueryable<TSource> source,
        PaginationOptions opts,
        CancellationToken ct = default)
    {
        if (_globalOptions is not null)
            opts = opts.ClampPageSize(_globalOptions.MaxPageSize);

        var pagedSource = await source.ToPagedAsync(opts, ct).ConfigureAwait(false);
        return MapPaged(pagedSource);
    }

    public IPagedResult<TDest> MapPaged(IPagedResult<TSource> source)
    {
        var mappedItems = _mapper.MapList<TSource, TDest>(source.Items);
        
        return new PagedResult<TDest>(mappedItems, source.TotalCount, source.PageNumber, source.PageSize);
    }

    public async Task<ICursorPagedResult<TDest>> MapCursorPagedAsync(
        IQueryable<TSource> source,
        CursorPaginationOptions opts,
        CancellationToken ct = default)
    {
        if (_globalOptions is not null)
            opts = opts.ClampPageSize(_globalOptions.MaxPageSize);

        var pagedSource = await source.ToCursorPagedAsync(opts, ct).ConfigureAwait(false);
        var mappedItems = _mapper.MapList<TSource, TDest>(pagedSource.Items);

        return new CursorPagedResult<TDest>(
            mappedItems,
            pagedSource.StartCursor,
            pagedSource.EndCursor,
            pagedSource.HasNextPage,
            pagedSource.HasPreviousPage,
            pagedSource.TotalCount);
    }
}
