using System;
using MapFlux;
using MapFlux.Pagination.Abstractions;
using MapFlux.Pagination.Extensions;
using MapFlux.Pagination.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MapFlux.Pagination.Tests;

public class GlobalOptionsTests
{
    private class User { public int Id { get; set; } }
    private class UserDto { public int Id { get; set; } }

    private class UserProfile : Profile
    {
        public override void Configure(IMapperConfigurationExpression cfg)
        {
            cfg.CreateMap<User, UserDto>(opt => { });
        }
    }

    [Fact]
    public void AddMapFluxPagination_WithGlobalOptions_ShouldRegisterServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddMapFluxPagination(
            cfg => cfg.AddProfile<UserProfile>(),
            opts =>
            {
                opts.DefaultPageSize = 25;
                opts.MaxPageSize = 50;
            });

        var provider = services.BuildServiceProvider();

        // Assert
        var mapper = provider.GetService<IMapper>();
        Assert.NotNull(mapper);

        var globalOptions = provider.GetService<PaginationGlobalOptions>();
        Assert.NotNull(globalOptions);
        Assert.Equal(25, globalOptions.DefaultPageSize);
        Assert.Equal(50, globalOptions.MaxPageSize);

        var paginatedMapper = provider.GetService<IPaginatedMapper<User, UserDto>>();
        Assert.NotNull(paginatedMapper);
    }
}
