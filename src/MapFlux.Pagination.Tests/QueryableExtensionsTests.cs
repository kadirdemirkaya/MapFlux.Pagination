using MapFlux.Pagination.Extensions;
using MapFlux.Pagination.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace MapFlux.Pagination.Tests;

public class QueryableExtensionsTests
{
    private class TestEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private class TestDbContext : DbContext
    {
        public DbSet<TestEntity> Entities => Set<TestEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseInMemoryDatabase("testdb_" + System.Guid.NewGuid());
    }

    [Fact]
    public async Task ToPagedAsync_ShouldReturn_CorrectPage()
    {
        // Arrange
        using var context = new TestDbContext();
        var entities = Enumerable.Range(1, 25).Select(i => new TestEntity { Id = i, Name = $"Item{i}" });
        context.Entities.AddRange(entities);
        await context.SaveChangesAsync();

        var opts = new PaginationOptions { PageNumber = 2, PageSize = 10 };

        // Act
        var result = await context.Entities.AsQueryable().ToPagedAsync(opts);

        // Assert
        Assert.Equal(25, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
        Assert.Equal(2, result.PageNumber);
        Assert.Equal(10, result.Items.Count);
        Assert.Equal(11, result.Items.First().Id); // 2. sayfa ilk elemanı
    }

    [Fact]
    public async Task ToPagedAsync_ShouldApply_Sorting()
    {
        // Arrange
        using var context = new TestDbContext();
        var entities = new[]
        {
            new TestEntity { Id = 1, Name = "Zebra" },
            new TestEntity { Id = 2, Name = "Apple" },
            new TestEntity { Id = 3, Name = "Mango" }
        };
        context.Entities.AddRange(entities);
        await context.SaveChangesAsync();

        var opts = new PaginationOptions { PageNumber = 1, PageSize = 10, SortBy = "Name", SortDescending = false };

        // Act
        var result = await context.Entities.AsQueryable().ToPagedAsync(opts);

        // Assert
        Assert.Equal("Apple", result.Items[0].Name);
        Assert.Equal("Mango", result.Items[1].Name);
        Assert.Equal("Zebra", result.Items[2].Name);
    }

    private class TestDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private class TestProfile : Profile
    {
        public override void Configure(IMapperConfigurationExpression cfg)
        {
            cfg.CreateMap<TestEntity, TestDto>(opt => { });
        }
    }

    [Fact]
    public async Task ToPagedAsync_WithMapping_ShouldMapAndPaginate()
    {
        // Arrange
        using var context = new TestDbContext();
        var mapper = new Mapper();
        mapper.CreateMap<TestProfile>();

        var entities = Enumerable.Range(1, 15).Select(i => new TestEntity { Id = i, Name = $"Item{i}" });
        context.Entities.AddRange(entities);
        await context.SaveChangesAsync();

        var opts = new PaginationOptions { PageNumber = 1, PageSize = 5 };

        // Act
        var result = await context.Entities.AsQueryable().ToPagedAsync<TestEntity, TestDto>(mapper, opts);

        // Assert
        Assert.Equal(15, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
        Assert.Equal(5, result.Items.Count);
        Assert.All(result.Items, item => Assert.NotEqual(0, item.Id));
    }
}
