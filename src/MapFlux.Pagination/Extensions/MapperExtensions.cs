using MapFlux;

namespace MapFlux.Pagination.Extensions;

/// <summary>Extension methods that map a sequence of items through a MapFlux <see cref="IMapper"/>.</summary>
public static class MapperExtensions
{
    /// <summary>Maps every item in <paramref name="source"/> from <typeparamref name="TSource"/> to <typeparamref name="TDest"/>.</summary>
    /// <typeparam name="TSource">The source item type.</typeparam>
    /// <typeparam name="TDest">The destination item type.</typeparam>
    /// <param name="mapper">The mapper used to project each item.</param>
    /// <param name="source">The items to map.</param>
    /// <returns>The mapped items.</returns>
    public static List<TDest> MapList<TSource, TDest>(this IMapper mapper, IEnumerable<TSource> source)
    {
        return source.Select(mapper.Map<TSource, TDest>).ToList();
    }
}
