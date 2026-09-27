using System.Collections.Generic;
using System.Linq;
using MapFlux.Pagination.Extensions;
using MapFlux.Pagination.Models;
using Xunit;

namespace MapFlux.Pagination.Tests;

public class InMemoryPipelineTests
{
    private class TestItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
    }

    private static List<TestItem> Seed()
        => Enumerable.Range(1, 20)
            .Select(i => new TestItem { Id = i, Name = $"Item{i}", Age = i })
            .ToList();

    private static PaginationOptions PipelineOptions()
        => new()
        {
            SortBy = "Id",
            SortDescending = true,
            Filters = new List<FilterCriteria>
            {
                new() { PropertyName = "Age", Operator = FilterOperator.Equals, Value = 5 }
            },
            SearchTerm = "zzz"
        };

    [Fact]
    public void ToPaged_Default_IgnoresFilterSearchAndSort()
    {
        var result = Seed().ToPaged(PipelineOptions());

        Assert.Equal(20, result.TotalCount);
        Assert.Equal(1, result.Items.First().Id);
    }

    [Fact]
    public void ToPaged_ApplyPipelineTrue_AppliesFilterSearchAndSort()
    {
        var result = Seed().ToPaged(PipelineOptions(), applyPipeline: true);

        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Items);
    }

    [Fact]
    public void ToPaged_ApplyPipelineTrue_FilterMatchesAndOrdersDescending()
    {
        var opts = new PaginationOptions
        {
            SortBy = "Id",
            SortDescending = true,
            Filters = new List<FilterCriteria>
            {
                new() { PropertyName = "Age", Operator = FilterOperator.GreaterThan, Value = 15 }
            }
        };

        var result = Seed().ToPaged(opts, applyPipeline: true);

        Assert.Equal(5, result.TotalCount);
        Assert.Equal(20, result.Items.First().Id);
        Assert.Equal(16, result.Items.Last().Id);
    }
}
