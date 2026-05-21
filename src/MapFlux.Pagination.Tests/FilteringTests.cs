using System.Collections.Generic;
using System.Linq;
using MapFlux.Pagination.Core;
using MapFlux.Pagination.Models;
using Xunit;

namespace MapFlux.Pagination.Tests;

public class FilteringTests
{
    private class TestItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public bool IsActive { get; set; }
    }

    private readonly IQueryable<TestItem> _items = new List<TestItem>
    {
        new() { Id = 1, Name = "Alice", Age = 25, IsActive = true },
        new() { Id = 2, Name = "Bob", Age = 30, IsActive = false },
        new() { Id = 3, Name = "Charlie", Age = 35, IsActive = true },
        new() { Id = 4, Name = "Dave", Age = 40, IsActive = false }
    }.AsQueryable();

    [Fact]
    public void ApplyFiltering_EqualsOperator_ShouldFilterCorrectly()
    {
        var filters = new List<FilterCriteria>
        {
            new() { PropertyName = "IsActive", Operator = FilterOperator.Equals, Value = true }
        };

        var query = QueryableHelper.ApplyFiltering(_items, filters);
        var result = query.ToList();

        Assert.Equal(2, result.Count);
        Assert.All(result, item => Assert.True(item.IsActive));
    }

    [Fact]
    public void ApplyFiltering_NotEqualsOperator_ShouldFilterCorrectly()
    {
        var filters = new List<FilterCriteria>
        {
            new() { PropertyName = "Age", Operator = FilterOperator.NotEquals, Value = 30 }
        };

        var query = QueryableHelper.ApplyFiltering(_items, filters);
        var result = query.ToList();

        Assert.Equal(3, result.Count);
        Assert.DoesNotContain(result, item => item.Age == 30);
    }

    [Fact]
    public void ApplyFiltering_GreaterThanOperator_ShouldFilterCorrectly()
    {
        var filters = new List<FilterCriteria>
        {
            new() { PropertyName = "Age", Operator = FilterOperator.GreaterThan, Value = 30 }
        };

        var query = QueryableHelper.ApplyFiltering(_items, filters);
        var result = query.ToList();

        Assert.Equal(2, result.Count);
        Assert.All(result, item => Assert.True(item.Age > 30));
    }

    [Fact]
    public void ApplyFiltering_ContainsOperator_ShouldFilterCorrectly()
    {
        var filters = new List<FilterCriteria>
        {
            new() { PropertyName = "Name", Operator = FilterOperator.Contains, Value = "a" }
        };

        var query = QueryableHelper.ApplyFiltering(_items, filters);
        var result = query.ToList();

        Assert.Equal(2, result.Count); 

        Assert.Contains(result, item => item.Name == "Charlie");
        Assert.Contains(result, item => item.Name == "Dave");
    }

    [Fact]
    public void ApplyFiltering_StartsWithOperator_ShouldFilterCorrectly()
    {
        var filters = new List<FilterCriteria>
        {
            new() { PropertyName = "Name", Operator = FilterOperator.StartsWith, Value = "Ch" }
        };

        var query = QueryableHelper.ApplyFiltering(_items, filters);
        var result = query.ToList();

        Assert.Single(result);
        Assert.Equal("Charlie", result.First().Name);
    }

    [Fact]
    public void ApplyFiltering_EndsWithOperator_ShouldFilterCorrectly()
    {
        var filters = new List<FilterCriteria>
        {
            new() { PropertyName = "Name", Operator = FilterOperator.EndsWith, Value = "e" }
        };

        var query = QueryableHelper.ApplyFiltering(_items, filters);
        var result = query.ToList();

        Assert.Equal(3, result.Count); // Alice, Charlie, Dave
        Assert.Contains(result, item => item.Name == "Alice");
        Assert.Contains(result, item => item.Name == "Charlie");
        Assert.Contains(result, item => item.Name == "Dave");
    }

    [Fact]
    public void ApplyFiltering_MultipleFilters_ShouldApplyAndLogicalOperators()
    {
        var filters = new List<FilterCriteria>
        {
            new() { PropertyName = "Age", Operator = FilterOperator.GreaterThanOrEqual, Value = 30 },
            new() { PropertyName = "IsActive", Operator = FilterOperator.Equals, Value = false }
        };

        var query = QueryableHelper.ApplyFiltering(_items, filters);
        var result = query.ToList();

        Assert.Equal(2, result.Count); // Bob (30, false), Dave (40, false)
        Assert.Contains(result, item => item.Name == "Bob");
        Assert.Contains(result, item => item.Name == "Dave");
    }
}
