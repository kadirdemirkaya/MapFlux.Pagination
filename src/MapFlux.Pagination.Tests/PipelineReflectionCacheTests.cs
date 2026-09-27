using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using MapFlux.Pagination.Core;
using MapFlux.Pagination.Models;
using Xunit;

namespace MapFlux.Pagination.Tests;

public class PipelineReflectionCacheTests
{
    private const BindingFlags LookupFlags = BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance;

    private class CacheItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int Age { get; set; }
        public bool IsActive { get; set; }
    }

    private class BaseRow
    {
        public int Rank { get; set; }
    }

    private class DerivedRow : BaseRow
    {
        public new int Rank { get; set; }
        public int Id { get; set; }
    }

    private class CaseCollisionRow
    {
        public int Id { get; set; }
        public int Rank { get; set; }
        public int rank { get; set; }
    }

    private static IQueryable<CacheItem> Items() => new List<CacheItem>
    {
        new() { Id = 1, Name = "Alice", Email = "alice@example.com", Age = 25, IsActive = true },
        new() { Id = 2, Name = "Bob", Email = "bob@example.com", Age = 30, IsActive = false },
        new() { Id = 3, Name = "Charlie", Email = "charlie@example.com", Age = 35, IsActive = true },
        new() { Id = 4, Name = "Daniel", Email = "daniel@example.com", Age = 40, IsActive = true }
    }.AsQueryable();

    private static PaginationOptions FullPipelineOptions() => new()
    {
        PageNumber = 1,
        PageSize = 10,
        Filters = new List<FilterCriteria>
        {
            new() { PropertyName = "isactive", Operator = FilterOperator.Equals, Value = true },
            new() { PropertyName = "AGE", Operator = FilterOperator.GreaterThanOrEqual, Value = 25 },
            new() { PropertyName = "NaMe", Operator = FilterOperator.Contains, Value = "i" }
        },
        SearchTerm = "LI",
        SortBy = "iD",
        SortDescending = true
    };

    [Fact]
    public void ApplyFullPipeline_RepeatedCalls_ProduceIdenticalExpressionTreesAndResults()
    {
        var options = FullPipelineOptions();

        var first = QueryableHelper.ApplyFullPipeline(Items(), options);
        var second = QueryableHelper.ApplyFullPipeline(Items(), options);

        Assert.Equal(first.Expression.ToString(), second.Expression.ToString());
        Assert.Equal(first.Select(i => i.Id).ToList(), second.Select(i => i.Id).ToList());
    }

    [Fact]
    public void ApplyFullPipeline_MixedCasePropertyNames_FilterSearchAndSortStillApply()
    {
        var result = QueryableHelper.ApplyFullPipeline(Items(), FullPipelineOptions()).ToList();

        Assert.Equal(new[] { 3, 1 }, result.Select(i => i.Id).ToArray());
    }

    [Fact]
    public void ApplySearch_WithoutSearchProperties_MatchesEveryPublicStringProperty()
    {
        var byName = QueryableHelper.ApplySearch(Items(), "charlie", null).ToList();
        var byEmail = QueryableHelper.ApplySearch(Items(), "BOB@EXAMPLE", null).ToList();

        Assert.Equal(new[] { 3 }, byName.Select(i => i.Id).ToArray());
        Assert.Equal(new[] { 2 }, byEmail.Select(i => i.Id).ToArray());
    }

    [Fact]
    public void ApplySearch_NamedPropertyInAnyCase_RestrictsToThatProperty()
    {
        var result = QueryableHelper
            .ApplySearch(Items(), "example", new[] { "eMaIl" })
            .ToList();

        Assert.Equal(4, result.Count);

        var nameOnly = QueryableHelper
            .ApplySearch(Items(), "example", new[] { "nAmE" })
            .ToList();

        Assert.Empty(nameOnly);
    }

    [Fact]
    public void ApplyMultiSorting_MixedCasePropertyNames_OrdersByEveryCriteria()
    {
        var criterias = new List<SortCriteria>
        {
            new() { PropertyName = "isACTIVE", Descending = false },
            new() { PropertyName = "aGe", Descending = true }
        };

        var result = QueryableHelper.ApplyMultiSorting(Items(), criterias).ToList();

        Assert.Equal(new[] { 2, 4, 3, 1 }, result.Select(i => i.Id).ToArray());
    }

    [Fact]
    public void ApplySorting_HiddenProperty_ResolvesTheSamePropertyAsReflection()
    {
        var expected = typeof(DerivedRow).GetProperty("rank", LookupFlags);

        Assert.NotNull(expected);
        Assert.Equal(typeof(DerivedRow), expected!.DeclaringType);

        var rows = new List<DerivedRow>
        {
            Row(id: 1, derivedRank: 3, baseRank: 1),
            Row(id: 2, derivedRank: 1, baseRank: 3),
            Row(id: 3, derivedRank: 2, baseRank: 2)
        }.AsQueryable();

        var result = QueryableHelper.ApplySorting(rows, "rank", descending: false).ToList();

        Assert.Equal(new[] { 2, 3, 1 }, result.Select(r => r.Id).ToArray());
    }

    [Fact]
    public void ApplySorting_PropertiesDifferingOnlyInCase_BehavesLikeReflection()
    {
        Assert.Throws<AmbiguousMatchException>(() => typeof(CaseCollisionRow).GetProperty("rank", LookupFlags));

        var rows = new List<CaseCollisionRow>
        {
            new() { Id = 1, Rank = 2, rank = 1 }
        }.AsQueryable();

        Assert.Throws<AmbiguousMatchException>(() => QueryableHelper.ApplySorting(rows, "rank", descending: false));
        Assert.Throws<AmbiguousMatchException>(() => QueryableHelper.ApplySorting(rows, "RANK", descending: false));
    }

    [Fact]
    public void ApplyFiltering_PropertiesDifferingOnlyInCase_BehavesLikeReflection()
    {
        var rows = new List<CaseCollisionRow>
        {
            new() { Id = 1, Rank = 2, rank = 1 }
        }.AsQueryable();

        var filters = new List<FilterCriteria>
        {
            new() { PropertyName = "rank", Operator = FilterOperator.Equals, Value = 1 }
        };

        Assert.Throws<AmbiguousMatchException>(() => QueryableHelper.ApplyFiltering(rows, filters));
    }

    [Fact]
    public void ApplySorting_UnrelatedPropertyOnCollidingType_StillResolves()
    {
        var rows = new List<CaseCollisionRow>
        {
            new() { Id = 2, Rank = 1, rank = 1 },
            new() { Id = 1, Rank = 2, rank = 2 }
        }.AsQueryable();

        var result = QueryableHelper.ApplySorting(rows, "iD", descending: false).ToList();

        Assert.Equal(new[] { 1, 2 }, result.Select(r => r.Id).ToArray());
    }

    [Fact]
    public async Task ApplyFullPipeline_UsedConcurrently_ProducesTheSameResultOnEveryThread()
    {
        var options = FullPipelineOptions();
        var expected = QueryableHelper.ApplyFullPipeline(Items(), options).Select(i => i.Id).ToList();

        var tasks = Enumerable.Range(0, 32).Select(_ => Task.Run(() =>
            QueryableHelper.ApplyFullPipeline(Items(), options).Select(i => i.Id).ToList()));

        var results = await Task.WhenAll(tasks);

        Assert.All(results, actual => Assert.Equal(expected, actual));
    }

    [Fact]
    public void ApplyCursorFilter_MixedCaseCursorProperty_StillPagesForward()
    {
        var cursor = QueryableHelper.EncodeCursor(2);

        var opts = new CursorPaginationOptions
        {
            PageSize = 2,
            CursorProperty = "iD",
            After = cursor
        };

        var result = QueryableHelper.ApplyCursorFilter(Items(), opts).ToList();

        Assert.Equal(new[] { 3, 4 }, result.Select(i => i.Id).ToArray());
    }

    private static DerivedRow Row(int id, int derivedRank, int baseRank)
    {
        var row = new DerivedRow { Id = id, Rank = derivedRank };
        ((BaseRow)row).Rank = baseRank;
        return row;
    }
}
