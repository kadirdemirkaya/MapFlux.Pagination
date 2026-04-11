using MapFlux.Pagination.Core;
using Xunit;

namespace MapFlux.Pagination.Tests;

public class MapperConfigurationBuilderTests
{
    private class Source { public int Id { get; set; } }
    private class Dest { public int Id { get; set; } }

    private class TestProfile : Profile
    {
        public override void Configure(IMapperConfigurationExpression cfg)
        {
            cfg.CreateMap<Source, Dest>(opt => { });
        }
    }

    private class AnotherProfile : Profile
    {
        public override void Configure(IMapperConfigurationExpression cfg)
        {
            cfg.CreateMap<Dest, Source>(opt => { });
        }
    }

    [Fact]
    public void AddProfile_ShouldRegisterProfile()
    {
        // Arrange
        var mapper = new Mapper();
        var builder = new MapperConfigurationBuilder(mapper);

        // Act
        builder.AddProfile<TestProfile>();

        // Assert - mapping should work after adding profile
        var source = new Source { Id = 5 };
        var dest = mapper.Map<Source, Dest>(source);
        Assert.Equal(5, dest.Id);
    }

    [Fact]
    public void AddProfile_Chained_ShouldRegisterMultipleProfiles()
    {
        // Arrange
        var mapper = new Mapper();
        var builder = new MapperConfigurationBuilder(mapper);

        // Act - chain multiple profiles
        builder
            .AddProfile<TestProfile>()
            .AddProfile<AnotherProfile>();

        // Assert - both mappings should work
        var source = new Source { Id = 10 };
        var dest = mapper.Map<Source, Dest>(source);
        var backToSource = mapper.Map<Dest, Source>(dest);

        Assert.Equal(10, dest.Id);
        Assert.Equal(10, backToSource.Id);
    }
}
