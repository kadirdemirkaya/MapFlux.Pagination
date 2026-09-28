using MapFlux;

namespace MapFlux.Pagination.Core;

/// <summary>
/// Registers MapFlux <see cref="Profile"/>s against the <see cref="Mapper"/> configured by
/// <c>AddMapFluxPagination</c>.
/// </summary>
public class MapperConfigurationBuilder
{
    private readonly Mapper _mapper;

    /// <summary>
    /// Creates a builder that registers profiles on <paramref name="mapper"/>.
    /// </summary>
    /// <param name="mapper">The mapper profiles are registered on.</param>
    public MapperConfigurationBuilder(Mapper mapper)
    {
        _mapper = mapper;
    }

    /// <summary>
    /// Registers the mapping profile <typeparamref name="T"/> on the underlying mapper.
    /// </summary>
    /// <typeparam name="T">The profile type to register.</typeparam>
    /// <returns>This builder, for fluent chaining.</returns>
    public MapperConfigurationBuilder AddProfile<T>() where T : Profile, new()
    {
        _mapper.CreateMap<T>();
        return this;
    }
}
