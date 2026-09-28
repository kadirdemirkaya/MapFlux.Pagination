using MapFlux;
using MapFlux.Pagination.Abstractions;
using MapFlux.Pagination.Models;
using MapFlux.Pagination.Extensions;

namespace MapFlux.Pagination.Core;

/// <summary>
/// Default <see cref="IPaginatedMapper{TSource, TDest}"/> implementation over a MapFlux <see cref="IMapper"/>.
/// </summary>
/// <typeparam name="TSource">The source entity type read from the query.</typeparam>
/// <typeparam name="TDest">The destination type each item is mapped to.</typeparam>
public class PaginatedMapper<TSource, TDest> : IPaginatedMapper<TSource, TDest>
{
    private readonly IMapper _mapper;
    private readonly PaginationGlobalOptions? _globalOptions;

    /// <summary>
    /// Creates a mapper with no <see cref="PaginationGlobalOptions"/> applied.
    /// </summary>
    /// <param name="mapper">The mapper used to project each page's items.</param>
    public PaginatedMapper(IMapper mapper)
        : this(mapper, null)
    {
    }

    /// <summary>
    /// Creates a mapper that clamps page size and resolves default ordering from
    /// <paramref name="globalOptions"/>.
    /// </summary>
    /// <param name="mapper">The mapper used to project each page's items.</param>
    /// <param name="globalOptions">The global options applied to every request, or <see langword="null"/>.</param>
    public PaginatedMapper(IMapper mapper, PaginationGlobalOptions? globalOptions)
    {
        _mapper = mapper;
        _globalOptions = globalOptions;
    }

    /// <inheritdoc />
    public async Task<IPagedResult<TDest>> MapPagedAsync(
        IQueryable<TSource> source,
        PaginationOptions opts,
        CancellationToken ct = default)
    {
        if (_globalOptions is not null)
        {
            opts = opts.ClampPageSize(_globalOptions.MaxPageSize);
            opts = ResolveDefaultOrdering(opts, _globalOptions);
        }

        var pagedSource = await source.ToPagedAsync(opts, ct).ConfigureAwait(false);
        return MapPaged(pagedSource);
    }

    private static PaginationOptions ResolveDefaultOrdering(PaginationOptions opts, PaginationGlobalOptions globalOptions)
    {
        var resolved = opts;

        if (string.IsNullOrWhiteSpace(resolved.DefaultSortProperty)
            && !string.IsNullOrWhiteSpace(globalOptions.DefaultSortProperty))
            resolved = resolved with { DefaultSortProperty = globalOptions.DefaultSortProperty };

        if (!resolved.EnsureDeterministicOrder && globalOptions.EnsureDeterministicOrder)
            resolved = resolved with { EnsureDeterministicOrder = true };

        return resolved;
    }

    /// <inheritdoc />
    public IPagedResult<TDest> MapPaged(IPagedResult<TSource> source)
    {
        var mappedItems = _mapper.MapList<TSource, TDest>(source.Items);
        
        return new PagedResult<TDest>(mappedItems, source.TotalCount, source.PageNumber, source.PageSize);
    }

    /// <inheritdoc />
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
