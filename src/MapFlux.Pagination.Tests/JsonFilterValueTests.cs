using MapFlux.Pagination.Core;
using MapFlux.Pagination.Extensions;
using MapFlux.Pagination.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace MapFlux.Pagination.Tests;

public class JsonFilterValueTests
{
    private class TestEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public int? OptionalAge { get; set; }
        public bool IsActive { get; set; }
        public bool? IsVerified { get; set; }
        public long Population { get; set; }
        public short Level { get; set; }
        public byte Rank { get; set; }
        public decimal Price { get; set; }
        public double Score { get; set; }
        public float Ratio { get; set; }
    }

    private class TestDbContext : DbContext
    {
        public DbSet<TestEntity> Entities => Set<TestEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseInMemoryDatabase("jsonfilter_" + Guid.NewGuid());
    }

    private static TestEntity Create(int i) => new()
    {
        Id = i,
        Name = "Item" + i,
        Age = i,
        OptionalAge = i % 2 == 0 ? i : null,
        IsActive = i % 2 == 1,
        IsVerified = i % 2 == 0 ? true : null,
        Population = i,
        Level = (short)i,
        Rank = (byte)i,
        Price = i + 0.5m,
        Score = i + 0.25d,
        Ratio = i + 0.5f
    };

    private static readonly List<TestEntity> Entities = Enumerable.Range(1, 20).Select(Create).ToList();

    private readonly IQueryable<TestEntity> _items = Entities.AsQueryable();

    private static FilterCriteria FromJson(string json)
        => JsonSerializer.Deserialize<FilterCriteria>(json)!;

    private static FilterCriteria FromJson(string propertyName, FilterOperator op, string jsonValue)
        => FromJson($"{{\"PropertyName\":\"{propertyName}\",\"Operator\":{(int)op},\"Value\":{jsonValue}}}");

    private List<TestEntity> Filter(params FilterCriteria[] filters)
        => QueryableHelper.ApplyFiltering(_items, filters).ToList();

    private static FilterCriteria WithRawValue(string propertyName, FilterOperator op, string rawJson)
        => new()
        {
            PropertyName = propertyName,
            Operator = op,
            Value = JsonDocument.Parse(rawJson).RootElement.Clone()
        };

    [Fact]
    public void ApplyFiltering_JsonDeserializedNumber_ShouldNarrowResult()
    {
        var filter = FromJson("{\"PropertyName\":\"Age\",\"Operator\":0,\"Value\":5}");

        Assert.IsType<JsonElement>(filter.Value);

        var result = Filter(filter);

        Assert.Single(result);
        Assert.Equal(5, result[0].Age);
    }

    [Fact]
    public async Task ToPagedAsync_JsonDeserializedNumber_ShouldNarrowResult()
    {
        using var context = new TestDbContext();
        context.Entities.AddRange(Enumerable.Range(1, 20).Select(Create));
        await context.SaveChangesAsync();

        var filter = FromJson("{\"PropertyName\":\"Age\",\"Operator\":0,\"Value\":5}");
        var options = new PaginationOptions { PageNumber = 1, PageSize = 10, Filters = new[] { filter } };

        var result = await context.Entities.AsQueryable().ToPagedAsync(options);

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal(5, result.Items[0].Age);
    }

    [Theory]
    [InlineData("Age", "5")]
    [InlineData("Population", "5")]
    [InlineData("Level", "5")]
    [InlineData("Rank", "5")]
    [InlineData("Price", "5.5")]
    [InlineData("Score", "5.25")]
    [InlineData("Ratio", "5.5")]
    [InlineData("OptionalAge", "6")]
    public void ApplyFiltering_JsonNumberValue_ShouldConvertToTargetType(string propertyName, string jsonValue)
    {
        var result = Filter(FromJson(propertyName, FilterOperator.Equals, jsonValue));

        Assert.Single(result);
    }

    [Fact]
    public void ApplyFiltering_JsonNumberValue_ShouldCompareWithGreaterThan()
    {
        var result = Filter(FromJson("Age", FilterOperator.GreaterThan, "15"));

        Assert.Equal(5, result.Count);
        Assert.All(result, item => Assert.True(item.Age > 15));
    }

    [Fact]
    public void ApplyFiltering_JsonNumberValue_OnStringMember_ShouldUseTextForm()
    {
        var result = Filter(FromJson("Name", FilterOperator.Contains, "1"));

        Assert.Equal(11, result.Count);
        Assert.All(result, item => Assert.Contains("1", item.Name));
    }

    [Fact]
    public void ApplyFiltering_JsonNumberValue_OutOfTargetRange_ShouldNotFilter()
    {
        var result = Filter(FromJson("Rank", FilterOperator.Equals, "99999"));

        Assert.Equal(20, result.Count);
    }

    [Fact]
    public void ApplyFiltering_JsonStringValue_OnStringMember_ShouldFilter()
    {
        var result = Filter(FromJson("{\"PropertyName\":\"Name\",\"Operator\":0,\"Value\":\"Item7\"}"));

        Assert.Single(result);
        Assert.Equal("Item7", result[0].Name);
    }

    [Theory]
    [InlineData(FilterOperator.Contains, "\"tem15\"", 1)]
    [InlineData(FilterOperator.StartsWith, "\"Item2\"", 2)]
    [InlineData(FilterOperator.EndsWith, "\"9\"", 2)]
    public void ApplyFiltering_JsonStringValue_ShouldSupportTextOperators(FilterOperator op, string jsonValue, int expected)
    {
        var result = Filter(FromJson("Name", op, jsonValue));

        Assert.Equal(expected, result.Count);
    }

    [Theory]
    [InlineData("Age", "\"5\"")]
    [InlineData("Population", "\"5\"")]
    [InlineData("Level", "\"5\"")]
    [InlineData("Rank", "\"5\"")]
    [InlineData("OptionalAge", "\"6\"")]
    public void ApplyFiltering_JsonStringValue_OnNumericMember_ShouldConvert(string propertyName, string jsonValue)
    {
        var result = Filter(FromJson(propertyName, FilterOperator.Equals, jsonValue));

        Assert.Single(result);
    }

    [Fact]
    public void ApplyFiltering_JsonStringValue_OnBooleanMember_ShouldConvert()
    {
        var result = Filter(FromJson("IsActive", FilterOperator.Equals, "\"true\""));

        Assert.Equal(10, result.Count);
        Assert.All(result, item => Assert.True(item.IsActive));
    }

    [Fact]
    public void ApplyFiltering_JsonTrueValue_ShouldFilter()
    {
        var result = Filter(FromJson("{\"PropertyName\":\"IsActive\",\"Operator\":0,\"Value\":true}"));

        Assert.Equal(10, result.Count);
        Assert.All(result, item => Assert.True(item.IsActive));
    }

    [Fact]
    public void ApplyFiltering_JsonFalseValue_ShouldFilter()
    {
        var result = Filter(FromJson("{\"PropertyName\":\"IsActive\",\"Operator\":0,\"Value\":false}"));

        Assert.Equal(10, result.Count);
        Assert.All(result, item => Assert.False(item.IsActive));
    }

    [Fact]
    public void ApplyFiltering_JsonTrueValue_OnNullableBooleanMember_ShouldFilter()
    {
        var result = Filter(FromJson("IsVerified", FilterOperator.Equals, "true"));

        Assert.Equal(10, result.Count);
        Assert.All(result, item => Assert.True(item.IsVerified));
    }

    [Fact]
    public void ApplyFiltering_JsonTrueValue_OnStringMember_ShouldUseTextForm()
    {
        var result = Filter(FromJson("Name", FilterOperator.Equals, "true"));

        Assert.Empty(result);
    }

    [Fact]
    public void ApplyFiltering_JsonNullValue_OnNullableMember_ShouldMatchNulls()
    {
        var result = Filter(WithRawValue("OptionalAge", FilterOperator.Equals, "null"));

        Assert.Equal(10, result.Count);
        Assert.All(result, item => Assert.Null(item.OptionalAge));
    }

    [Fact]
    public void ApplyFiltering_JsonNullValue_WithNotEquals_OnNullableMember_ShouldMatchNonNulls()
    {
        var result = Filter(WithRawValue("OptionalAge", FilterOperator.NotEquals, "null"));

        Assert.Equal(10, result.Count);
        Assert.All(result, item => Assert.NotNull(item.OptionalAge));
    }

    [Fact]
    public void ApplyFiltering_JsonNullValue_OnNonNullableMember_ShouldNotFilter()
    {
        var result = Filter(WithRawValue("Age", FilterOperator.Equals, "null"));

        Assert.Equal(20, result.Count);
    }

    [Theory]
    [InlineData("{\"Age\":5}")]
    [InlineData("[5]")]
    public void ApplyFiltering_JsonStructuredValue_ShouldNotFilter(string rawJson)
    {
        var result = Filter(WithRawValue("Age", FilterOperator.Equals, rawJson));

        Assert.Equal(20, result.Count);
    }

    [Fact]
    public void ApplyFiltering_JsonDeserializedFilters_ShouldCombineWithAndLogic()
    {
        var result = Filter(
            FromJson("Age", FilterOperator.GreaterThan, "15"),
            FromJson("IsActive", FilterOperator.Equals, "true"));

        Assert.Equal(2, result.Count);
        Assert.All(result, item =>
        {
            Assert.True(item.Age > 15);
            Assert.True(item.IsActive);
        });
    }
}
