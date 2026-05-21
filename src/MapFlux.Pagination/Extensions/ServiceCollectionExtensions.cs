using MapFlux;
using MapFlux.Pagination.Abstractions;
using MapFlux.Pagination.Core;
using MapFlux.Pagination.Models;
using Microsoft.Extensions.DependencyInjection;

namespace MapFlux.Pagination.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMapFluxPagination(
        this IServiceCollection services, 
        Action<MapperConfigurationBuilder>? configure = null)
    {
        // 1. Register MapFlux core mapper (singleton - profiles are immutable)
        services.AddSingleton<IMapper>(sp => 
        {
            var m = new Mapper();
            configure?.Invoke(new MapperConfigurationBuilder(m));
            return m;
        });

        // 2. Register the generic paginated mapper (open generic)
        services.AddScoped(
            typeof(IPaginatedMapper<,>), 
            typeof(PaginatedMapper<,>));

        return services;
    }

    public static IServiceCollection AddMapFluxPagination(
        this IServiceCollection services,
        Action<MapperConfigurationBuilder>? configure,
        Action<PaginationGlobalOptions>? globalOptions)
    {
        // 1. Register global options
        var options = new PaginationGlobalOptions();
        globalOptions?.Invoke(options);
        services.AddSingleton(options);

        // 2. Register core services
        services.AddMapFluxPagination(configure);

        return services;
    }
}
