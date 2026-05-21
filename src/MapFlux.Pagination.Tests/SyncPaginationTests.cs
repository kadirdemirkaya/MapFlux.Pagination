using System.Collections.Generic;
using System.Linq;
using MapFlux;
using MapFlux.Pagination.Extensions;
using MapFlux.Pagination.Models;
using Xunit;

namespace MapFlux.Pagination.Tests;

public class SyncPaginationTests
{
    private class Entity { public int Id { get; set; } public string Name { get; set; } = string.Empty; }
    private class Dto { public int Id { get; set; } public string Name { get; set; } = string.Empty; }

    private class TestProfile : Profile
    {
        public override void Configure(IMapperConfigurationExpression cfg)
        {
            cfg.CreateMap<Entity, Dto>(opt => { });
        }
    }

    [Fact]
    public void ToPaged_Sync_ShouldPaginateCorrectly()
    {
        var entities = Enumerable.Range(1, 25).Select(i => new Entity { Id = i, Name = $"Item{i}" }).ToList();
        var opts = new PaginationOptions { PageNumber = 2, PageSize = 10 };

        var paged = entities.ToPaged(opts);

        Assert.Equal(25, paged.TotalCount);
        Assert.Equal(3, paged.TotalPages);
        Assert.Equal(10, paged.Items.Count);
        Assert.Equal(11, paged.Items.First().Id);
    }

    [Fact]
    public void ToPaged_SyncWithMapping_ShouldPaginateAndMapCorrectly()
    {
        var mapper = new Mapper();
        mapper.CreateMap<TestProfile>();

        var entities = Enumerable.Range(1, 15).Select(i => new Entity { Id = i, Name = $"Item{i}" }).ToList();
        var opts = new PaginationOptions { PageNumber = 3, PageSize = 5 };

        var paged = entities.ToPaged<Entity, Dto>(mapper, opts);

        Assert.Equal(15, paged.TotalCount);
        Assert.Equal(3, paged.TotalPages);
        Assert.Equal(5, paged.Items.Count);
        Assert.IsType<Dto>(paged.Items.First());
        Assert.Equal(11, paged.Items.First().Id);
    }
}
