using MapFlux;

namespace MapFlux.Pagination.Extensions;

public static class MapperExtensions
{
    public static List<TDest> MapList<TSource, TDest>(this IMapper mapper, IEnumerable<TSource> source)
    {
        return source.Select(mapper.Map<TSource, TDest>).ToList();
    }
}
