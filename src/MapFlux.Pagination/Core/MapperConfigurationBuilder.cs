using MapFlux;

namespace MapFlux.Pagination.Core;

public class MapperConfigurationBuilder
{
    private readonly Mapper _mapper;

    public MapperConfigurationBuilder(Mapper mapper)
    {
        _mapper = mapper;
    }

    public MapperConfigurationBuilder AddProfile<T>() where T : Profile, new()
    {
        _mapper.CreateMap<T>();
        return this;
    }
}
