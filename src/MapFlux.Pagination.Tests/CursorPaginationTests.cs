using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MapFlux;
using MapFlux.Pagination.Core;
using MapFlux.Pagination.Extensions;
using MapFlux.Pagination.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MapFlux.Pagination.Tests;

public class CursorPaginationTests
{
    private class TestEntity
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    private class TestDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
    }

    private class TestProfile : Profile
    {
        public override void Configure(IMapperConfigurationExpression cfg)
        {
            cfg.CreateMap<TestEntity, TestDto>(opt => { });
        }
    }

    private class TestDbContext : DbContext
    {
        public DbSet<TestEntity> Entities => Set<TestEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseInMemoryDatabase("cursor_testdb_" + Guid.NewGuid());
    }

    [Fact]
    public async Task CursorPagedAsync_FirstPage_ShouldCalculateCursorsCorrectly()
    {
        using var context = new TestDbContext();
        var entities = Enumerable.Range(1, 15).Select(i => new TestEntity { Id = i, Code = $"C{i}" });
        context.Entities.AddRange(entities);
        await context.SaveChangesAsync();

        var opts = new CursorPaginationOptions { PageSize = 5, CursorProperty = "Id" };

        var result = await context.Entities.AsQueryable().ToCursorPagedAsync(opts);

        Assert.Equal(15, result.TotalCount);
        Assert.Equal(5, result.Items.Count);
        Assert.True(result.HasNextPage);
        Assert.False(result.HasPreviousPage);

        // First value = 1, last value = 5
        var expectedStart = QueryableHelper.EncodeCursor(1);
        var expectedEnd = QueryableHelper.EncodeCursor(5);

        Assert.Equal(expectedStart, result.StartCursor);
        Assert.Equal(expectedEnd, result.EndCursor);
    }

    [Fact]
    public async Task CursorPagedAsync_NextPage_ShouldFilterUsingAfterCursor()
    {
        using var context = new TestDbContext();
        var entities = Enumerable.Range(1, 15).Select(i => new TestEntity { Id = i, Code = $"C{i}" });
        context.Entities.AddRange(entities);
        await context.SaveChangesAsync();

        // Get first page to obtain cursor
        var firstPageOpts = new CursorPaginationOptions { PageSize = 5, CursorProperty = "Id" };
        var firstPage = await context.Entities.AsQueryable().ToCursorPagedAsync(firstPageOpts);

        // Get second page
        var secondPageOpts = new CursorPaginationOptions
        {
            PageSize = 5,
            CursorProperty = "Id",
            After = firstPage.EndCursor
        };
        var secondPage = await context.Entities.AsQueryable().ToCursorPagedAsync(secondPageOpts);

        Assert.Equal(5, secondPage.Items.Count);
        Assert.Equal(6, secondPage.Items.First().Id);
        Assert.Equal(10, secondPage.Items.Last().Id);
        Assert.True(secondPage.HasNextPage);
        Assert.True(secondPage.HasPreviousPage);
    }

    [Fact]
    public async Task CursorPagedAsync_WithMapping_ShouldMapItems()
    {
        using var context = new TestDbContext();
        var mapper = new Mapper();
        mapper.CreateMap<TestProfile>();

        var entities = Enumerable.Range(1, 5).Select(i => new TestEntity { Id = i, Code = $"C{i}" });
        context.Entities.AddRange(entities);
        await context.SaveChangesAsync();

        var opts = new CursorPaginationOptions { PageSize = 3, CursorProperty = "Id" };

        var result = await context.Entities.AsQueryable().ToCursorPagedAsync<TestEntity, TestDto>(mapper, opts);

        Assert.Equal(5, result.TotalCount);
        Assert.Equal(3, result.Items.Count);
        Assert.IsType<TestDto>(result.Items.First());
        Assert.Equal("C1", result.Items.First().Code);
    }
}
