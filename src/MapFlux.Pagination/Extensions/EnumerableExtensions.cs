using MapFlux;
using MapFlux.Pagination.Models;

namespace MapFlux.Pagination.Extensions;

public static class EnumerableExtensions
{
    public static IPagedResult<T> ToPaged<T>(
        this IEnumerable<T> source,
        PaginationOptions opts)
    {
        var list = source.ToList();
        var total = list.Count;
        var items = list.Skip(opts.Skip).Take(opts.Take).ToList();

        return new PagedResult<T>(items, total, opts.PageNumber, opts.PageSize);
    }

    public static IPagedResult<TDest> ToPaged<TSource, TDest>(
        this IEnumerable<TSource> source,
        IMapper mapper,
        PaginationOptions opts)
    {
        var paged = source.ToPaged(opts);
        var mapped = paged.Items.Select(e => mapper.Map<TSource, TDest>(e)).ToList();

        return new PagedResult<TDest>(mapped, paged.TotalCount, paged.PageNumber, paged.PageSize);
    }
}
