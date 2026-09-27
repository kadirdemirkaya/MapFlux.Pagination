using System.Collections.Generic;
using System.Linq;
using MapFlux.Pagination.Core;
using MapFlux.Pagination.Exceptions;
using MapFlux.Pagination.Models;
using Xunit;

namespace MapFlux.Pagination.Tests;

public class StrictModeTests
{
    private class TestItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
    }

    private static IQueryable<TestItem> Seed()
        => Enumerable.Range(1, 20)
            .Select(i => new TestItem { Id = i, Name = $"Item{i}", Age = i })
            .ToList()
            .AsQueryable();

    [Fact]
    public void ApplyFiltering_UnconvertibleValue_Default_ShouldDropFilterAndReturnAllRows()
    {
        var filters = new List<FilterCriteria>
        {
            new() { PropertyName = "Age", Operator = FilterOperator.Equals, Value = "abc" }
        };

        var result = QueryableHelper.ApplyFiltering(Seed(), filters).ToList();

        Assert.Equal(20, result.Count);
    }

    [Fact]
    public void ApplyFiltering_UnconvertibleValue_Strict_ShouldThrowWithPropertyValueAndType()
    {
        var filters = new List<FilterCriteria>
        {
            new() { PropertyName = "Age", Operator = FilterOperator.Equals, Value = "abc" }
        };

        var ex = Assert.Throws<PaginationStrictModeException>(
            () => QueryableHelper.ApplyFiltering(Seed(), filters, strict: true).ToList());

        Assert.Equal("Age", ex.PropertyName);
        Assert.Equal("abc", ex.Value);
        Assert.Equal(typeof(int), ex.TargetType);
    }

    [Fact]
    public void ApplyFiltering_UnknownProperty_Default_ShouldDropFilterAndReturnAllRows()
    {
        var filters = new List<FilterCriteria>
        {
            new() { PropertyName = "Agee", Operator = FilterOperator.Equals, Value = 5 }
        };

        var result = QueryableHelper.ApplyFiltering(Seed(), filters).ToList();

        Assert.Equal(20, result.Count);
    }

    [Fact]
    public void ApplyFiltering_UnknownProperty_Strict_ShouldThrowWithPropertyName()
    {
        var filters = new List<FilterCriteria>
        {
            new() { PropertyName = "Agee", Operator = FilterOperator.Equals, Value = 5 }
        };

        var ex = Assert.Throws<PaginationStrictModeException>(
            () => QueryableHelper.ApplyFiltering(Seed(), filters, strict: true).ToList());

        Assert.Equal("Agee", ex.PropertyName);
    }

    [Fact]
    public void ApplyFiltering_OperatorNotSupportedForType_Default_ShouldDropFilterAndReturnAllRows()
    {
        var filters = new List<FilterCriteria>
        {
            new() { PropertyName = "Age", Operator = FilterOperator.Contains, Value = 1 }
        };

        var result = QueryableHelper.ApplyFiltering(Seed(), filters).ToList();

        Assert.Equal(20, result.Count);
    }

    [Fact]
    public void ApplyFiltering_OperatorNotSupportedForType_Strict_ShouldThrowWithPropertyAndTargetType()
    {
        var filters = new List<FilterCriteria>
        {
            new() { PropertyName = "Age", Operator = FilterOperator.Contains, Value = 1 }
        };

        var ex = Assert.Throws<PaginationStrictModeException>(
            () => QueryableHelper.ApplyFiltering(Seed(), filters, strict: true).ToList());

        Assert.Equal("Age", ex.PropertyName);
        Assert.Equal(typeof(int), ex.TargetType);
    }

    [Fact]
    public void ApplySearch_UnknownSearchProperty_Default_ShouldIgnorePropertyAndReturnAllRows()
    {
        var result = QueryableHelper.ApplySearch(Seed(), "Item1", new[] { "Nope" }).ToList();

        Assert.Equal(20, result.Count);
    }

    [Fact]
    public void ApplySearch_UnknownSearchProperty_Strict_ShouldThrowWithPropertyName()
    {
        var ex = Assert.Throws<PaginationStrictModeException>(
            () => QueryableHelper.ApplySearch(Seed(), "Item1", new[] { "Nope" }, strict: true).ToList());

        Assert.Equal("Nope", ex.PropertyName);
    }

    [Fact]
    public void ApplySorting_UnknownProperty_Strict_ShouldThrowWithPropertyName()
    {
        var ex = Assert.Throws<PaginationStrictModeException>(
            () => QueryableHelper.ApplySorting(Seed(), "Nme", false, strict: true).ToList());

        Assert.Equal("Nme", ex.PropertyName);
    }

    [Fact]
    public void ApplyMultiSorting_UnknownProperty_Strict_ShouldThrowWithPropertyName()
    {
        var criterias = new List<SortCriteria>
        {
            new() { PropertyName = "Nope", Descending = false }
        };

        var ex = Assert.Throws<PaginationStrictModeException>(
            () => QueryableHelper.ApplyMultiSorting(Seed(), criterias, strict: true).ToList());

        Assert.Equal("Nope", ex.PropertyName);
    }

    [Fact]
    public void ApplyFullPipeline_StrictModeOff_ShouldKeepTodaysSilentBehaviour()
    {
        var opts = new PaginationOptions
        {
            Filters = new List<FilterCriteria>
            {
                new() { PropertyName = "Agee", Operator = FilterOperator.Equals, Value = 5 }
            },
            SearchTerm = "Item1",
            SearchProperties = new List<string> { "Nope" }
        };

        var result = QueryableHelper.ApplyFullPipeline(Seed(), opts).ToList();

        Assert.Equal(20, result.Count);
    }

    [Fact]
    public void ApplyFullPipeline_StrictModeOn_ShouldThrowForUnknownFilterProperty()
    {
        var opts = new PaginationOptions
        {
            StrictMode = true,
            Filters = new List<FilterCriteria>
            {
                new() { PropertyName = "Agee", Operator = FilterOperator.Equals, Value = 5 }
            }
        };

        Assert.Throws<PaginationStrictModeException>(() => QueryableHelper.ApplyFullPipeline(Seed(), opts).ToList());
    }
}
