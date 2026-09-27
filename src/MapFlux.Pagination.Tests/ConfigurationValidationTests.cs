using System;
using MapFlux;
using MapFlux.Pagination.Abstractions;
using MapFlux.Pagination.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MapFlux.Pagination.Tests;

public class ConfigurationValidationTests
{
    private class Widget { public int Id { get; set; } public string Name { get; set; } = string.Empty; }
    private class WidgetDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    private class BrokenWidgetProfile : Profile
    {
        public override void Configure(IMapperConfigurationExpression cfg)
        {
            cfg.CreateMap<Widget, WidgetDto>(opt => { });
        }
    }

    private class ValidWidgetProfile : Profile
    {
        public override void Configure(IMapperConfigurationExpression cfg)
        {
            cfg.CreateMap<Widget, Widget>(opt => { });
        }
    }

    [Fact]
    public void ValidateOnStart_WithIncompleteMap_ShouldThrowWhenMapperIsResolved()
    {
        var services = new ServiceCollection();
        services.AddMapFluxPagination(
            cfg => cfg.AddProfile<BrokenWidgetProfile>(),
            opts => opts.ValidateOnStart = true);

        var provider = services.BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IMapper>());
    }

    [Fact]
    public void ValidateOnStart_Disabled_ShouldNotThrowForIncompleteMapUntilUsed()
    {
        var services = new ServiceCollection();
        services.AddMapFluxPagination(cfg => cfg.AddProfile<BrokenWidgetProfile>());

        var provider = services.BuildServiceProvider();

        var mapper = provider.GetRequiredService<IMapper>();
        Assert.NotNull(mapper);
    }

    [Fact]
    public void ValidateOnStart_WithValidMap_ShouldNotThrow()
    {
        var services = new ServiceCollection();
        services.AddMapFluxPagination(
            cfg => cfg.AddProfile<ValidWidgetProfile>(),
            opts => opts.ValidateOnStart = true);

        var provider = services.BuildServiceProvider();

        var mapper = provider.GetRequiredService<IMapper>();
        Assert.NotNull(mapper);
    }
}
