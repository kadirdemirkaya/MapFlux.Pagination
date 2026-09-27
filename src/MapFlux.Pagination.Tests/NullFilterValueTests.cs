using System.Collections.Generic;
using System.Linq;
using MapFlux.Pagination.Core;
using MapFlux.Pagination.Exceptions;
using MapFlux.Pagination.Models;
using Xunit;

namespace MapFlux.Pagination.Tests;

public class NullFilterValueTests
{
    private class TestItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public int? OptionalAge { get; set; }
    }

    private static IQueryable<TestItem> Seed()
        => Enumerable.Range(1, 20)
            .Select(i => new TestItem { Id = i, Name = $"Item{i}", Age = i, OptionalAge = i % 2 == 0 ? i : null })
            .ToList()
            .AsQueryable();

    [Fact]
    public void ApplyFiltering_NullValue_EqualsOnNonNullableMember_Default_ShouldMatchNoRows()
    {
        var filters = new List<FilterCriteria>
        {
            new() { PropertyName = "Age", Operator = FilterOperator.Equals, Value = null }
        };

        var result = QueryableHelper.ApplyFiltering(Seed(), filters).ToList();

        Assert.Empty(result);
    }

    [Fact]
    public void ApplyFiltering_NullValue_NotEqualsOnNonNullableMember_Default_ShouldMatchAllRows()
    {
        var filters = new List<FilterCriteria>
        {
            new() { PropertyName = "Age", Operator = FilterOperator.NotEquals, Value = null }
        };

        var result = QueryableHelper.ApplyFiltering(Seed(), filters).ToList();

        Assert.Equal(20, result.Count);
    }

    [Fact]
    public void ApplyFiltering_NullValue_EqualsOnNonNullableMember_Strict_ShouldThrowWithPropertyAndType()
    {
        var filters = new List<FilterCriteria>
        {
            new() { PropertyName = "Age", Operator = FilterOperator.Equals, Value = null }
        };

        var ex = Assert.Throws<PaginationStrictModeException>(
            () => QueryableHelper.ApplyFiltering(Seed(), filters, strict: true).ToList());

        Assert.Equal("Age", ex.PropertyName);
        Assert.Null(ex.Value);
        Assert.Equal(typeof(int), ex.TargetType);
    }

    [Fact]
    public void ApplyFiltering_NullValue_NotEqualsOnNonNullableMember_Strict_ShouldThrowWithPropertyAndType()
    {
        var filters = new List<FilterCriteria>
        {
            new() { PropertyName = "Age", Operator = FilterOperator.NotEquals, Value = null }
        };

        var ex = Assert.Throws<PaginationStrictModeException>(
            () => QueryableHelper.ApplyFiltering(Seed(), filters, strict: true).ToList());

        Assert.Equal("Age", ex.PropertyName);
        Assert.Equal(typeof(int), ex.TargetType);
    }

    [Fact]
    public void ApplyFiltering_NullValue_EqualsOnNullableMember_ShouldMatchNulls()
    {
        var filters = new List<FilterCriteria>
        {
            new() { PropertyName = "OptionalAge", Operator = FilterOperator.Equals, Value = null }
        };

        var result = QueryableHelper.ApplyFiltering(Seed(), filters).ToList();

        Assert.Equal(10, result.Count);
        Assert.All(result, item => Assert.Null(item.OptionalAge));
    }

    [Fact]
    public void ApplyFiltering_NullValue_NotEqualsOnNullableMember_ShouldMatchNonNulls()
    {
        var filters = new List<FilterCriteria>
        {
            new() { PropertyName = "OptionalAge", Operator = FilterOperator.NotEquals, Value = null }
        };

        var result = QueryableHelper.ApplyFiltering(Seed(), filters).ToList();

        Assert.Equal(10, result.Count);
        Assert.All(result, item => Assert.NotNull(item.OptionalAge));
    }
}
