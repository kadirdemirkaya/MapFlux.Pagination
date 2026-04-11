using MapFlux.Pagination.Extensions;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace MapFlux.Pagination.Tests;

public class MapperExtensionsTests
{
    private class Source { public int Id { get; set; } public string Name { get; set; } = string.Empty; }
    private class Dest { public int Id { get; set; } public string Name { get; set; } = string.Empty; }

    private class TestProfile : Profile
    {
        public override void Configure(IMapperConfigurationExpression cfg)
        {
            cfg.CreateMap<Source, Dest>(opt => { });
        }
    }

    [Fact]
    public void MapList_ShouldMap_AllItems()
    {
        // Arrange
        var mapper = new Mapper();
        mapper.CreateMap<TestProfile>();

        var sourceItems = new List<Source>
        {
            new() { Id = 1, Name = "A" },
            new() { Id = 2, Name = "B" },
            new() { Id = 3, Name = "C" }
        };

        // Act
        var result = mapper.MapList<Source, Dest>(sourceItems);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal("A", result[0].Name);
        Assert.Equal("B", result[1].Name);
        Assert.Equal("C", result[2].Name);
    }

    [Fact]
    public void MapList_WithEmptyList_ShouldReturnEmpty()
    {
        // Arrange
        var mapper = new Mapper();
        mapper.CreateMap<TestProfile>();
        var emptyList = new List<Source>();

        // Act
        var result = mapper.MapList<Source, Dest>(emptyList);

        // Assert
        Assert.Empty(result);
    }
}
