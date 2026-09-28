using MapFlux;
using MapFlux.Pagination.Core;
using MapFlux.Pagination.Models;

namespace MapFlux.Pagination.Extensions;

/// <summary>In-memory offset pagination over an <see cref="IEnumerable{T}"/>.</summary>
public static class EnumerableExtensions
{
    /// <summary>
    /// Pages <paramref name="source"/> in memory by <see cref="PaginationOptions.Skip"/>/<see cref="PaginationOptions.Take"/>
    /// only. Filters, search and sort are not applied.
    /// </summary>
    /// <param name="source">The sequence to page.</param>
    /// <param name="opts">The pagination options the page is read with.</param>
    /// <returns>The requested page.</returns>
    public static IPagedResult<T> ToPaged<T>(
        this IEnumerable<T> source,
        PaginationOptions opts)
        => ToPaged<T>(source, opts, applyPipeline: false);

    /// <summary>
    /// Pages <paramref name="source"/> in memory, optionally applying the full filter/search/sort pipeline first.
    /// </summary>
    /// <param name="source">The sequence to page.</param>
    /// <param name="opts">The pagination options the page is read with.</param>
    /// <param name="applyPipeline">When <see langword="true"/>, applies <see cref="QueryableHelper.ApplyFullPipeline{T}"/> before paging; when <see langword="false"/>, only <see cref="PaginationOptions.Skip"/>/<see cref="PaginationOptions.Take"/> are applied.</param>
    /// <returns>The requested page.</returns>
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

    /// <summary>
    /// Pages <paramref name="source"/> in memory by <see cref="PaginationOptions.Skip"/>/<see cref="PaginationOptions.Take"/>
    /// only, then maps each item to <typeparamref name="TDest"/>.
    /// </summary>
    /// <param name="source">The sequence to page.</param>
    /// <param name="mapper">The mapper used to project each page item.</param>
    /// <param name="opts">The pagination options the page is read with.</param>
    /// <returns>The requested, mapped page.</returns>
    public static IPagedResult<TDest> ToPaged<TSource, TDest>(
        this IEnumerable<TSource> source,
        IMapper mapper,
        PaginationOptions opts)
        => ToPaged<TSource, TDest>(source, mapper, opts, applyPipeline: false);

    /// <summary>
    /// Pages <paramref name="source"/> in memory, optionally applying the full filter/search/sort pipeline
    /// first, then maps each item to <typeparamref name="TDest"/>.
    /// </summary>
    /// <param name="source">The sequence to page.</param>
    /// <param name="mapper">The mapper used to project each page item.</param>
    /// <param name="opts">The pagination options the page is read with.</param>
    /// <param name="applyPipeline">When <see langword="true"/>, applies <see cref="QueryableHelper.ApplyFullPipeline{T}"/> before paging; when <see langword="false"/>, only <see cref="PaginationOptions.Skip"/>/<see cref="PaginationOptions.Take"/> are applied.</param>
    /// <returns>The requested, mapped page.</returns>
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
