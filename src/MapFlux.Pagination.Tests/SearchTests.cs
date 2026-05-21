using System.Collections.Generic;
using System.Linq;
using MapFlux.Pagination.Core;
using Xunit;

namespace MapFlux.Pagination.Tests;

public class SearchTests
{
    private class TestItem
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    private readonly IQueryable<TestItem> _items = new List<TestItem>
    {
        new() { Title = "Apple Laptop", Description = "A silver computer made by Apple", Count = 5 },
        new() { Title = "Banana Fruit", Description = "Yellow organic banana", Count = 10 },
        new() { Title = "Orange Juice", Description = "Fresh orange drink, better than apple juice", Count = 1 },
        new() { Title = "Dell Desktop", Description = "Windows desktop machine", Count = 3 },
        new() { Title = "Apple Pie", Description = "Warm dessert", Count = 2 }
    }.AsQueryable();

    [Fact]
    public void ApplySearch_AllProperties_ShouldFindMatch()
    {
        // Should search across all string properties by default
        var query = QueryableHelper.ApplySearch(_items, "apple", null);
        var result = query.ToList();

        Assert.Equal(3, result.Count);
        Assert.Contains(result, item => item.Title == "Apple Laptop");
        Assert.Contains(result, item => item.Title == "Orange Juice");
        Assert.Contains(result, item => item.Title == "Apple Pie");
    }

    [Fact]
    public void ApplySearch_SpecificProperties_ShouldOnlySearchThose()
    {
        // Only search Title property (matches "Apple Laptop" and "Apple Pie")
        var query = QueryableHelper.ApplySearch(_items, "apple", new[] { "Title" });
        var result = query.ToList();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, item => item.Title == "Apple Laptop");
        Assert.Contains(result, item => item.Title == "Apple Pie");
    }

    [Fact]
    public void ApplySearch_NullOrEmptySearchTerm_ShouldReturnAll()
    {
        var query = QueryableHelper.ApplySearch(_items, "", null);
        Assert.Equal(5, query.Count());

        query = QueryableHelper.ApplySearch(_items, null, null);
        Assert.Equal(5, query.Count());
    }
}
