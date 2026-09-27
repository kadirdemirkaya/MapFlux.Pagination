using System.Linq;
using System.Threading.Tasks;
using MapFlux.Pagination.Extensions;
using MapFlux.Pagination.Models;
using Xunit;

namespace MapFlux.Pagination.Tests;

public class NonAsyncProviderTests
{
    private class TestEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private static IQueryable<TestEntity> Seed(int count)
        => Enumerable.Range(1, count)
            .Select(i => new TestEntity { Id = i, Name = $"Item{i}" })
            .AsQueryable();

    [Fact]
    public async Task ToPagedAsync_ShouldReturn_CorrectPage_WhenProviderIsNotAsync()
    {
        var opts = new PaginationOptions { PageNumber = 2, PageSize = 10, SortBy = "Id" };

        var result = await Seed(25).ToPagedAsync(opts);

        Assert.Equal(25, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
        Assert.Equal(10, result.Items.Count);
        Assert.Equal(11, result.Items.First().Id);
    }

    [Fact]
    public async Task ToCursorPagedAsync_ShouldReturn_CorrectPage_WhenProviderIsNotAsync()
    {
        var opts = new CursorPaginationOptions { PageSize = 10, CursorProperty = "Id" };

        var result = await Seed(25).ToCursorPagedAsync(opts);

        Assert.Equal(25, result.TotalCount);
        Assert.Equal(10, result.Items.Count);
        Assert.Equal(1, result.Items.First().Id);
        Assert.Equal(10, result.Items.Last().Id);
        Assert.True(result.HasNextPage);
    }
}
