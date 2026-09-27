using MapFlux;
using MapFlux.Pagination.Core;
using MapFlux.Pagination.Models;

namespace MapFlux.Pagination.Extensions;

public static class EnumerableExtensions
{
    public static IPagedResult<T> ToPaged<T>(
        this IEnumerable<T> source,
        PaginationOptions opts)
        => ToPaged<T>(source, opts, applyPipeline: false);

    public static IPagedResult<T> ToPaged<T>(
        this IEnumerable<T> source,
        PaginationOptions opts,
        bool applyPipeline)
    {
        if (!applyPipeline)
        {
            var list = source.ToList();
            var total = list.Count;
            var items = list.Skip(opts.Skip).Take(opts.Take).ToList();

            return new PagedResult<T>(items, total, opts.PageNumber, opts.PageSize);
        }

        var query = QueryableHelper.ApplyFullPipeline(source.AsQueryable(), opts);
        var pipelineTotal = query.Count();
        var pipelineItems = query.Skip(opts.Skip).Take(opts.Take).ToList();

        return new PagedResult<T>(pipelineItems, pipelineTotal, opts.PageNumber, opts.PageSize);
    }

    public static IPagedResult<TDest> ToPaged<TSource, TDest>(
        this IEnumerable<TSource> source,
        IMapper mapper,
        PaginationOptions opts)
        => ToPaged<TSource, TDest>(source, mapper, opts, applyPipeline: false);

    public static IPagedResult<TDest> ToPaged<TSource, TDest>(
        this IEnumerable<TSource> source,
        IMapper mapper,
        PaginationOptions opts,
        bool applyPipeline)
    {
        var paged = ToPaged<TSource>(source, opts, applyPipeline);
        var mapped = paged.Items.Select(e => mapper.Map<TSource, TDest>(e)).ToList();

        return new PagedResult<TDest>(mapped, paged.TotalCount, paged.PageNumber, paged.PageSize);
    }
}
