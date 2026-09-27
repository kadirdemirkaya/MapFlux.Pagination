using System;
using System.Collections.Generic;
using System.Linq;
using MapFlux;
using MapFlux.Pagination.Abstractions;
using MapFlux.Pagination.Core;
using MapFlux.Pagination.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MapFlux.Pagination.Extensions;

public static class ServiceCollectionExtensions
{
    private sealed class PaginationRegistrationAccumulator
    {
        public List<Action<MapperConfigurationBuilder>> ConfigureActions { get; } = new();
        public List<Action<PaginationGlobalOptions>> GlobalOptionsActions { get; } = new();
    }

    private static PaginationRegistrationAccumulator GetOrAddAccumulator(this IServiceCollection services)
    {
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(PaginationRegistrationAccumulator));
        if (descriptor?.ImplementationInstance is PaginationRegistrationAccumulator existing)
            return existing;

        var accumulator = new PaginationRegistrationAccumulator();
        services.AddSingleton(accumulator);
        return accumulator;
    }

    public static IServiceCollection AddMapFluxPagination(
        this IServiceCollection services,
        Action<MapperConfigurationBuilder>? configure = null)
    {
        var accumulator = services.GetOrAddAccumulator();
        if (configure is not null)
            accumulator.ConfigureActions.Add(configure);

        services.TryAddSingleton<IMapper>(sp =>
        {
            var m = new Mapper();
            foreach (var configureAction in accumulator.ConfigureActions)
                configureAction.Invoke(new MapperConfigurationBuilder(m));
            return m;
        });

        services.TryAddScoped(
            typeof(IPaginatedMapper<,>),
            typeof(PaginatedMapper<,>));

        return services;
    }

    public static IServiceCollection AddMapFluxPagination(
        this IServiceCollection services,
        Action<MapperConfigurationBuilder>? configure,
        Action<PaginationGlobalOptions>? globalOptions)
    {
        var accumulator = services.GetOrAddAccumulator();
        if (globalOptions is not null)
            accumulator.GlobalOptionsActions.Add(globalOptions);

        services.TryAddSingleton(sp =>
        {
            var options = new PaginationGlobalOptions();
            foreach (var globalOptionsAction in accumulator.GlobalOptionsActions)
                globalOptionsAction.Invoke(options);
            return options;
        });

        services.AddMapFluxPagination(configure);

        return services;
    }
}
