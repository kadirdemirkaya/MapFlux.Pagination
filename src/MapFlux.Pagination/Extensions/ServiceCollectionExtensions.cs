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
        services.AddSingleton<IMapper>(sp =>
        {
            var m = new Mapper();
            configure?.Invoke(new MapperConfigurationBuilder(m));
            return m;
        });

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
        var options = new PaginationGlobalOptions();
        globalOptions?.Invoke(options);
        services.AddSingleton(options);

        services.AddMapFluxPagination(configure);

        return services;
    }
}
