using System.Collections.Generic;
using System.Linq;
using MapFlux.Pagination.Core;
using MapFlux.Pagination.Models;
using Xunit;

namespace MapFlux.Pagination.Tests;

public class MultiSortTests
{
    private class TestItem
    {
        public string Category { get; set; } = string.Empty;
        public int Value { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private readonly IQueryable<TestItem> _items = new List<TestItem>
    {
        new() { Category = "B", Value = 10, Name = "Zebra" },
        new() { Category = "A", Value = 20, Name = "Monkey" },
        new() { Category = "A", Value = 10, Name = "Apple" },
        new() { Category = "B", Value = 10, Name = "Cat" }
    }.AsQueryable();

    [Fact]
    public void ApplyMultiSorting_MultipleColumns_ShouldSortCorrectly()
    {
        var criterias = new List<SortCriteria>
        {
            new() { PropertyName = "Category", Descending = false }, // A, A, B, B
            new() { PropertyName = "Value", Descending = true },     // A(20), A(10), B(10), B(10)
            new() { PropertyName = "Name", Descending = false }       // B(10, Cat), B(10, Zebra)
        };

        var query = QueryableHelper.ApplyMultiSorting(_items, criterias);
        var result = query.ToList();

        Assert.Equal(4, result.Count);
        // Order should be:
        // 1st: Category = "A", Value = 20, Name = "Monkey"
        // 2nd: Category = "A", Value = 10, Name = "Apple"
        // 3rd: Category = "B", Value = 10, Name = "Cat"
        // 4th: Category = "B", Value = 10, Name = "Zebra"
        Assert.Equal("Monkey", result[0].Name);
        Assert.Equal("Apple", result[1].Name);
        Assert.Equal("Cat", result[2].Name);
        Assert.Equal("Zebra", result[3].Name);
    }

    [Fact]
    public void ApplyMultiSorting_EmptySortCriterias_ShouldNotSort()
    {
        var result = QueryableHelper.ApplyMultiSorting(_items, new List<SortCriteria>()).ToList();
        Assert.Equal("Zebra", result[0].Name); // Original order
    }
}
