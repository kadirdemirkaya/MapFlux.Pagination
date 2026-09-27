using MapFlux.Pagination.Core;
using MapFlux.Pagination.Extensions;
using MapFlux.Pagination.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace MapFlux.Pagination.Tests;

public class StringFilterValueTests
{
    private static readonly DateTime BaseInstant = new(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);

    private class TestEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public double Score { get; set; }
        public float Ratio { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTimeOffset Offset { get; set; }
        public Guid Key { get; set; }
        public Guid? OptionalKey { get; set; }
    }

    private class DatePartsEntity
    {
        public int Id { get; set; }
        public DateOnly Day { get; set; }
        public TimeOnly Moment { get; set; }
    }

    private class TestDbContext : DbContext
    {
        public DbSet<TestEntity> Entities => Set<TestEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseInMemoryDatabase("stringfilter_" + Guid.NewGuid());
    }

    private static Guid KeyOf(int i) => Guid.Parse($"{i:D8}-0000-0000-0000-000000000000");

    private static TestEntity Create(int i) => new()
    {
        Id = i,
        Name = "Item" + i,
        Price = i + 0.5m,
        Score = i + 0.25d,
        Ratio = i + 0.5f,
        CreatedAt = BaseInstant.AddHours(i - 10),
        Offset = new DateTimeOffset(BaseInstant.AddHours(i - 10)),
        Key = KeyOf(i),
        OptionalKey = i % 2 == 0 ? KeyOf(i) : null
    };

    private static DatePartsEntity CreateDateParts(int i) => new()
    {
        Id = i,
        Day = new DateOnly(2026, 1, 1).AddDays(i - 1),
        Moment = new TimeOnly(10, 0, 0).AddMinutes(i * 6)
    };

    private readonly IQueryable<TestEntity> _items = Enumerable.Range(1, 20).Select(Create).ToList().AsQueryable();

    private readonly IQueryable<DatePartsEntity> _dateParts = Enumerable.Range(1, 20).Select(CreateDateParts).ToList().AsQueryable();

    private static FilterCriteria Criteria(string propertyName, FilterOperator op, object value)
        => new() { PropertyName = propertyName, Operator = op, Value = value };

    private static List<T> Filter<T>(IQueryable<T> items, params FilterCriteria[] filters)
        => QueryableHelper.ApplyFiltering(items, filters).ToList();

    private static T UnderCulture<T>(string cultureName, Func<T> action)
    {
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo(cultureName);
        try
        {
            return action();
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void ApplyFiltering_StringDateTimeValue_WithUtcDesignator_ShouldNotShiftToLocalTime()
    {
        var result = Filter(_items, Criteria("CreatedAt", FilterOperator.GreaterThan, "2026-01-01T10:00:01Z"));

        Assert.Equal(10, result.Count);
        Assert.All(result, item => Assert.True(item.CreatedAt > BaseInstant.AddSeconds(1)));
    }

    [Fact]
    public void ApplyFiltering_StringDateTimeValue_ShouldMatchTheExactInstant()
    {
        var result = Filter(_items, Criteria("CreatedAt", FilterOperator.Equals, "2026-01-01T12:00:00Z"));

        Assert.Single(result);
        Assert.Equal(12, result[0].Id);
    }

    [Fact]
    public void ApplyFiltering_StringDateTimeValue_UnderTurkishCulture_ShouldStillMatch()
    {
        var result = UnderCulture("tr-TR", () => Filter(_items, Criteria("CreatedAt", FilterOperator.Equals, "2026-01-01T12:00:00Z")));

        Assert.Single(result);
        Assert.Equal(12, result[0].Id);
    }

    [Fact]
    public void ApplyFiltering_StringDateTimeValue_Invalid_ShouldNotFilter()
    {
        var result = Filter(_items, Criteria("CreatedAt", FilterOperator.Equals, "not-a-date"));

        Assert.Equal(20, result.Count);
    }

    [Fact]
    public void ApplyFiltering_StringDateTimeOffsetValue_WithUtcDesignator_ShouldCompareOnTheInstant()
    {
        var result = Filter(_items, Criteria("Offset", FilterOperator.GreaterThan, "2026-01-01T10:00:01Z"));

        Assert.Equal(10, result.Count);
        Assert.All(result, item => Assert.True(item.Offset > new DateTimeOffset(BaseInstant.AddSeconds(1))));
    }

    [Fact]
    public void ApplyFiltering_StringDateTimeOffsetValue_WithExplicitOffset_ShouldMatchTheSameInstant()
    {
        var result = Filter(_items, Criteria("Offset", FilterOperator.Equals, "2026-01-01T15:00:00+03:00"));

        Assert.Single(result);
        Assert.Equal(12, result[0].Id);
    }

    [Fact]
    public void ApplyFiltering_StringGuidValue_ShouldMatchSingleRow()
    {
        var result = Filter(_items, Criteria("Key", FilterOperator.Equals, KeyOf(7).ToString()));

        Assert.Single(result);
        Assert.Equal(7, result[0].Id);
    }

    [Fact]
    public void ApplyFiltering_StringGuidValue_WithBraces_ShouldMatchSingleRow()
    {
        var result = Filter(_items, Criteria("Key", FilterOperator.Equals, KeyOf(7).ToString("B")));

        Assert.Single(result);
        Assert.Equal(7, result[0].Id);
    }

    [Fact]
    public void ApplyFiltering_StringGuidValue_OnNullableMember_ShouldMatchSingleRow()
    {
        var result = Filter(_items, Criteria("OptionalKey", FilterOperator.Equals, KeyOf(8).ToString()));

        Assert.Single(result);
        Assert.Equal(8, result[0].Id);
    }

    [Fact]
    public void ApplyFiltering_StringGuidValue_Invalid_ShouldNotFilter()
    {
        var result = Filter(_items, Criteria("Key", FilterOperator.Equals, "not-a-guid"));

        Assert.Equal(20, result.Count);
    }

    [Theory]
    [InlineData("tr-TR")]
    [InlineData("de-DE")]
    [InlineData("en-US")]
    [InlineData("")]
    public void ApplyFiltering_StringDecimalValue_WithInvariantSeparator_ShouldMatchInEveryCulture(string cultureName)
    {
        var result = UnderCulture(cultureName, () => Filter(_items, Criteria("Price", FilterOperator.Equals, "5.5")));

        Assert.Single(result);
        Assert.Equal(5, result[0].Id);
    }

    [Theory]
    [InlineData("tr-TR")]
    [InlineData("de-DE")]
    public void ApplyFiltering_StringDecimalValue_WithCultureSeparator_ShouldKeepMatching(string cultureName)
    {
        var result = UnderCulture(cultureName, () => Filter(_items, Criteria("Price", FilterOperator.Equals, "5,5")));

        Assert.Single(result);
        Assert.Equal(5, result[0].Id);
    }

    [Fact]
    public void ApplyFiltering_StringDoubleValue_WithInvariantSeparator_ShouldMatchUnderTurkishCulture()
    {
        var result = UnderCulture("tr-TR", () => Filter(_items, Criteria("Score", FilterOperator.Equals, "5.25")));

        Assert.Single(result);
        Assert.Equal(5, result[0].Id);
    }

    [Fact]
    public void ApplyFiltering_StringSingleValue_WithInvariantSeparator_ShouldMatchUnderTurkishCulture()
    {
        var result = UnderCulture("tr-TR", () => Filter(_items, Criteria("Ratio", FilterOperator.Equals, "5.5")));

        Assert.Single(result);
        Assert.Equal(5, result[0].Id);
    }

    [Fact]
    public void ApplyFiltering_StringDecimalValue_WithGreaterThan_ShouldNarrowUnderTurkishCulture()
    {
        var result = UnderCulture("tr-TR", () => Filter(_items, Criteria("Price", FilterOperator.GreaterThan, "15.5")));

        Assert.Equal(5, result.Count);
        Assert.All(result, item => Assert.True(item.Price > 15.5m));
    }

    [Fact]
    public void ApplyFiltering_StringDecimalValue_Invalid_ShouldNotFilter()
    {
        var result = Filter(_items, Criteria("Price", FilterOperator.Equals, "not-a-number"));

        Assert.Equal(20, result.Count);
    }

    [Fact]
    public void ApplyFiltering_StringIntegerValue_ShouldStillConvert()
    {
        var result = Filter(_items, Criteria("Id", FilterOperator.Equals, "5"));

        Assert.Single(result);
        Assert.Equal(5, result[0].Id);
    }

    [Fact]
    public void ApplyFiltering_StringDateOnlyValue_ShouldMatchSingleRow()
    {
        var result = Filter(_dateParts, Criteria("Day", FilterOperator.Equals, "2026-01-05"));

        Assert.Single(result);
        Assert.Equal(5, result[0].Id);
    }

    [Fact]
    public void ApplyFiltering_StringDateOnlyValue_UnderTurkishCulture_ShouldMatchSingleRow()
    {
        var result = UnderCulture("tr-TR", () => Filter(_dateParts, Criteria("Day", FilterOperator.Equals, "2026-01-05")));

        Assert.Single(result);
        Assert.Equal(5, result[0].Id);
    }

    [Fact]
    public void ApplyFiltering_StringDateOnlyValue_WithGreaterThan_ShouldNarrow()
    {
        var result = Filter(_dateParts, Criteria("Day", FilterOperator.GreaterThan, "2026-01-10"));

        Assert.Equal(10, result.Count);
        Assert.All(result, item => Assert.True(item.Day > new DateOnly(2026, 1, 10)));
    }

    [Fact]
    public void ApplyFiltering_StringTimeOnlyValue_ShouldMatchSingleRow()
    {
        var result = Filter(_dateParts, Criteria("Moment", FilterOperator.Equals, "10:30:00"));

        Assert.Single(result);
        Assert.Equal(5, result[0].Id);
    }

    [Fact]
    public void ApplyFiltering_StringTimeOnlyValue_Invalid_ShouldNotFilter()
    {
        var result = Filter(_dateParts, Criteria("Moment", FilterOperator.Equals, "not-a-time"));

        Assert.Equal(20, result.Count);
    }

    [Fact]
    public void ApplyFiltering_JsonStringValue_OnGuidMember_ShouldMatchSingleRow()
    {
        var value = JsonDocument.Parse($"\"{KeyOf(7)}\"").RootElement.Clone();

        var result = Filter(_items, Criteria("Key", FilterOperator.Equals, value));

        Assert.Single(result);
        Assert.Equal(7, result[0].Id);
    }

    [Fact]
    public void ApplyFiltering_JsonStringValue_OnDateTimeMember_ShouldNotShiftToLocalTime()
    {
        var value = JsonDocument.Parse("\"2026-01-01T12:00:00Z\"").RootElement.Clone();

        var result = Filter(_items, Criteria("CreatedAt", FilterOperator.Equals, value));

        Assert.Single(result);
        Assert.Equal(12, result[0].Id);
    }

    [Fact]
    public async Task ToPagedAsync_StringDateTimeValue_WithUtcDesignator_ShouldNotShiftToLocalTime()
    {
        using var context = new TestDbContext();
        context.Entities.AddRange(Enumerable.Range(1, 20).Select(Create));
        await context.SaveChangesAsync();

        var options = new PaginationOptions
        {
            PageNumber = 1,
            PageSize = 20,
            Filters = new[] { Criteria("CreatedAt", FilterOperator.GreaterThan, "2026-01-01T10:00:01Z") }
        };

        var result = await context.Entities.AsQueryable().ToPagedAsync(options);

        Assert.Equal(10, result.TotalCount);
        Assert.All(result.Items, item => Assert.True(item.CreatedAt > BaseInstant.AddSeconds(1)));
    }

    [Fact]
    public async Task ToPagedAsync_StringGuidValue_ShouldMatchSingleRow()
    {
        using var context = new TestDbContext();
        context.Entities.AddRange(Enumerable.Range(1, 20).Select(Create));
        await context.SaveChangesAsync();

        var options = new PaginationOptions
        {
            PageNumber = 1,
            PageSize = 20,
            Filters = new[] { Criteria("Key", FilterOperator.Equals, KeyOf(7).ToString()) }
        };

        var result = await context.Entities.AsQueryable().ToPagedAsync(options);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(7, result.Items[0].Id);
    }
}
