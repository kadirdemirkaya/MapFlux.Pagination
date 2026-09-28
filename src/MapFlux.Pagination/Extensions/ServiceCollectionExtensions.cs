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

/// <summary>Registers the pagination mapper and its <see cref="PaginationGlobalOptions"/> in an <see cref="IServiceCollection"/>.</summary>
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

    /// <summary>
    /// Registers a singleton <see cref="MapFlux.IMapper"/> configured by <paramref name="configure"/> and
    /// an open-generic scoped <c>IPaginatedMapper&lt;,&gt;</c>. Safe to call more than once: profiles from
    /// every call accumulate on the same mapper instead of the later registration replacing the earlier one.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="configure">Registers mapping profiles on the shared <see cref="MapperConfigurationBuilder"/>, or <see langword="null"/>.</param>
    /// <returns><paramref name="services"/>, for fluent chaining.</returns>
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

            if (sp.GetService<PaginationGlobalOptions>()?.ValidateOnStart == true)
                m.AssertConfigurationIsValid();

            return m;
        });

        services.TryAddScoped(
            typeof(IPaginatedMapper<,>),
            typeof(PaginatedMapper<,>));

        return services;
    }

    /// <summary>
    /// Registers the pagination mapper the same way as the single-argument <c>AddMapFluxPagination</c>
    /// overload, plus a singleton <see cref="PaginationGlobalOptions"/> configured by <paramref name="globalOptions"/>.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="configure">Registers mapping profiles on the shared <see cref="MapperConfigurationBuilder"/>, or <see langword="null"/>.</param>
    /// <param name="globalOptions">Configures the shared <see cref="PaginationGlobalOptions"/>, or <see langword="null"/>.</param>
    /// <returns><paramref name="services"/>, for fluent chaining.</returns>
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
