using System;
using System.Linq;
using System.Threading.Tasks;
using MapFlux.Pagination.Core;
using MapFlux.Pagination.Extensions;
using MapFlux.Pagination.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MapFlux.Pagination.Tests;

public class CursorComparableTypeTests
{
    private enum Priority
    {
        Lowest = 1,
        Low = 2,
        Normal = 3,
        High = 4,
        Highest = 5,
        Critical = 6
    }

    private class TestEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public Guid Key { get; set; }
        public Priority Priority { get; set; }
    }

    private class InMemoryContext : DbContext
    {
        public DbSet<TestEntity> Entities => Set<TestEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseInMemoryDatabase("cursor_comparable_" + Guid.NewGuid());
    }

    private class SqliteContext : DbContext
    {
        private readonly SqliteConnection _connection;

        public SqliteContext(SqliteConnection connection)
        {
            _connection = connection;
        }

        public DbSet<TestEntity> Entities => Set<TestEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlite(_connection);
    }

    private static readonly Guid[] Keys =
    {
        Guid.ParseExact("6b1f2c48-0000-4000-8000-000000000001", "D"),
        Guid.ParseExact("0f9c7a13-1111-4111-8111-111111111111", "D"),
        Guid.ParseExact("e2d4b6a8-2222-4222-8222-222222222222", "D"),
        Guid.ParseExact("3a5c7e90-3333-4333-8333-333333333333", "D"),
        Guid.ParseExact("c81b3d5f-4444-4444-8444-444444444444", "D"),
        Guid.ParseExact("94ae1602-5555-4555-8555-555555555555", "D"),
        Guid.ParseExact("172b8dfc-6666-4666-8666-666666666666", "D"),
        Guid.ParseExact("bd30f9a7-7777-4777-8777-777777777777", "D"),
        Guid.ParseExact("5e6a0c21-8888-4888-8888-888888888888", "D"),
        Guid.ParseExact("a047de83-9999-4999-8999-999999999999", "D"),
        Guid.ParseExact("2fb95174-aaaa-4aaa-8aaa-aaaaaaaaaaaa", "D"),
        Guid.ParseExact("d6284eb0-bbbb-4bbb-8bbb-bbbbbbbbbbbb", "D")
    };

    private static void Seed(DbContext context)
    {
        for (var i = 1; i <= Keys.Length; i++)
        {
            context.Add(new TestEntity
            {
                Id = i,
                Name = $"Item{i:D2}",
                Key = Keys[i - 1],
                Priority = (Priority)(((i - 1) % 6) + 1)
            });
        }

        context.SaveChanges();
    }

    private static InMemoryContext CreateInMemoryContext()
    {
        var context = new InMemoryContext();
        Seed(context);
        return context;
    }

    private static async Task<SqliteContext> CreateSqliteContextAsync(SqliteConnection connection)
    {
        await connection.OpenAsync();

        var context = new SqliteContext(connection);
        await context.Database.EnsureCreatedAsync();
        Seed(context);

        return context;
    }

    private static async Task<int[]> TwoPagesAsync(IQueryable<TestEntity> source, CursorPaginationOptions opts)
    {
        var firstPage = await source.ToCursorPagedAsync(opts);
        var secondPage = await source.ToCursorPagedAsync(opts with { After = firstPage.EndCursor });

        return firstPage.Items.Select(e => e.Id).Concat(secondPage.Items.Select(e => e.Id)).ToArray();
    }

    [Fact]
    public async Task CursorPagedAsync_StringCursor_ReturnsSecondPageWithoutRepeatingRows()
    {
        using var context = CreateInMemoryContext();

        var firstPage = await context.Entities.AsQueryable()
            .ToCursorPagedAsync(new CursorPaginationOptions { PageSize = 5, CursorProperty = "Name" });

        var secondPage = await context.Entities.AsQueryable()
            .ToCursorPagedAsync(new CursorPaginationOptions
            {
                PageSize = 5,
                CursorProperty = "Name",
                After = firstPage.EndCursor
            });

        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, firstPage.Items.Select(e => e.Id).ToArray());
        Assert.Equal(new[] { 6, 7, 8, 9, 10 }, secondPage.Items.Select(e => e.Id).ToArray());
        Assert.Equal("Item05", firstPage.Items.Last().Name);
    }

    [Fact]
    public async Task CursorPagedAsync_StringCursorWithBefore_AppliesLessThanComparison()
    {
        using var context = CreateInMemoryContext();

        var cursor = QueryableHelper.EncodeCursor("Item04");

        var page = await context.Entities.AsQueryable()
            .ToCursorPagedAsync(new CursorPaginationOptions
            {
                PageSize = 10,
                CursorProperty = "Name",
                Before = cursor
            });

        Assert.Equal(new[] { 1, 2, 3 }, page.Items.Select(e => e.Id).ToArray());
    }

    [Fact]
    public async Task CursorPagedAsync_StringCursorRoundTrip_ContinuesFromTheIssuedCursor()
    {
        using var context = CreateInMemoryContext();

        var cursor = QueryableHelper.EncodeCursor("Item10");

        var page = await context.Entities.AsQueryable()
            .ToCursorPagedAsync(new CursorPaginationOptions
            {
                PageSize = 3,
                CursorProperty = "Name",
                After = cursor
            });

        Assert.Equal(new[] { 11, 12 }, page.Items.Select(e => e.Id).ToArray());
        Assert.False(page.HasNextPage);
    }

    [Fact]
    public async Task CursorPagedAsync_GuidCursor_ReturnsPagesInKeyOrder()
    {
        using var context = CreateInMemoryContext();

        var expected = context.Entities.OrderBy(e => e.Key).Select(e => e.Id).Take(10).ToArray();

        var actual = await TwoPagesAsync(
            context.Entities.AsQueryable(),
            new CursorPaginationOptions { PageSize = 5, CursorProperty = "Key" });

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task CursorPagedAsync_EnumCursor_ReturnsPagesInValueOrder()
    {
        using var context = CreateInMemoryContext();

        var firstPage = await context.Entities.AsQueryable()
            .ToCursorPagedAsync(new CursorPaginationOptions { PageSize = 3, CursorProperty = "Priority" });

        var secondPage = await context.Entities.AsQueryable()
            .ToCursorPagedAsync(new CursorPaginationOptions
            {
                PageSize = 3,
                CursorProperty = "Priority",
                After = firstPage.EndCursor
            });

        Assert.Equal(new[] { Priority.Lowest, Priority.Lowest, Priority.Low }, firstPage.Items.Select(e => e.Priority).ToArray());
        Assert.All(secondPage.Items, entity => Assert.True(entity.Priority > Priority.Low));
    }

    [Fact]
    public async Task CursorPagedAsync_StringCursorOnRelationalProvider_IsTranslatedToSql()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        using var context = await CreateSqliteContextAsync(connection);

        var expected = context.Entities.OrderBy(e => e.Name).Select(e => e.Id).Take(10).ToArray();

        var actual = await TwoPagesAsync(
            context.Entities.AsQueryable(),
            new CursorPaginationOptions { PageSize = 5, CursorProperty = "Name" });

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task ApplyCursorFilter_StringCursorOnRelationalProvider_ComparesTheColumnInSql()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        using var context = await CreateSqliteContextAsync(connection);

        var sql = QueryableHelper.ApplyCursorFilter(
                context.Entities,
                new CursorPaginationOptions { PageSize = 5, CursorProperty = "Name", After = QueryableHelper.EncodeCursor("Item05") })
            .ToQueryString();

        Assert.Contains("\"Name\" >", sql);
    }

    [Fact]
    public async Task ApplyCursorFilter_GuidCursorOnRelationalProvider_ComparesTheColumnInSql()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        using var context = await CreateSqliteContextAsync(connection);

        var sql = QueryableHelper.ApplyCursorFilter(
                context.Entities,
                new CursorPaginationOptions { PageSize = 5, CursorProperty = "Key", After = QueryableHelper.EncodeCursor(Keys[0]) })
            .ToQueryString();

        Assert.Contains("\"Key\" >", sql);
    }

    [Fact]
    public async Task CursorPagedAsync_GuidCursorOnRelationalProvider_IsTranslatedToSql()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        using var context = await CreateSqliteContextAsync(connection);

        var expected = context.Entities.OrderBy(e => e.Key).Select(e => e.Id).Take(10).ToArray();

        var actual = await TwoPagesAsync(
            context.Entities.AsQueryable(),
            new CursorPaginationOptions { PageSize = 5, CursorProperty = "Key" });

        Assert.Equal(expected, actual);
    }
}
