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
    private class Order { public int Id { get; set; } }
    private class OrderDto { public int Id { get; set; } }

    private class UserProfile : Profile
    {
        public override void Configure(IMapperConfigurationExpression cfg)
        {
            cfg.CreateMap<User, UserDto>(opt => { });
        }
    }

    private class OrderProfile : Profile
    {
        public override void Configure(IMapperConfigurationExpression cfg)
        {
            cfg.CreateMap<Order, OrderDto>(opt => { });
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

    [Fact]
    public void AddMapFluxPagination_WithoutGlobalOptions_ShouldStillResolvePaginatedMapper()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddMapFluxPagination(cfg => cfg.AddProfile<UserProfile>());

        var provider = services.BuildServiceProvider();

        // Assert
        Assert.Null(provider.GetService<PaginationGlobalOptions>());

        var paginatedMapper = provider.GetService<IPaginatedMapper<User, UserDto>>();
        Assert.NotNull(paginatedMapper);
    }

    [Fact]
    public void AddMapFluxPagination_CalledTwice_ShouldKeepProfilesFromBothCalls()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddMapFluxPagination(cfg => cfg.AddProfile<UserProfile>());
        services.AddMapFluxPagination(cfg => cfg.AddProfile<OrderProfile>());

        var provider = services.BuildServiceProvider();
        var mapper = provider.GetRequiredService<IMapper>();

        // Assert
        var userDto = mapper.Map<User, UserDto>(new User { Id = 1 });
        Assert.Equal(1, userDto.Id);

        var orderDto = mapper.Map<Order, OrderDto>(new Order { Id = 2 });
        Assert.Equal(2, orderDto.Id);
    }

    [Fact]
    public void AddMapFluxPagination_WithGlobalOptionsCalledTwice_ShouldApplyBothConfigurations()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddMapFluxPagination(
            cfg => cfg.AddProfile<UserProfile>(),
            opts => opts.DefaultPageSize = 25);
        services.AddMapFluxPagination(
            cfg => cfg.AddProfile<OrderProfile>(),
            opts => opts.MaxPageSize = 50);

        var provider = services.BuildServiceProvider();

        // Assert
        var globalOptions = provider.GetRequiredService<PaginationGlobalOptions>();
        Assert.Equal(25, globalOptions.DefaultPageSize);
        Assert.Equal(50, globalOptions.MaxPageSize);

        var mapper = provider.GetRequiredService<IMapper>();
        var userDto = mapper.Map<User, UserDto>(new User { Id = 1 });
        Assert.Equal(1, userDto.Id);

        var orderDto = mapper.Map<Order, OrderDto>(new Order { Id = 2 });
        Assert.Equal(2, orderDto.Id);
    }
}
