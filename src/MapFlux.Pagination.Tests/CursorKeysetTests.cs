using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MapFlux.Pagination.Core;
using MapFlux.Pagination.Extensions;
using MapFlux.Pagination.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MapFlux.Pagination.Tests;

public class CursorKeysetTests
{
    private const int RowCount = 20;

    private class TestEntity
    {
        public int Id { get; set; }
        public int Score { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private class InMemoryContext : DbContext
    {
        public DbSet<TestEntity> Entities => Set<TestEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseInMemoryDatabase("cursor_keyset_" + Guid.NewGuid());
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

    private static void Seed(DbContext context)
    {
        for (var i = 1; i <= RowCount; i++)
        {
            context.Add(new TestEntity
            {
                Id = i,
                Score = ((i - 1) % 4) + 1,
                Name = $"Item{i:D2}"
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

    private static async Task<List<int>> WalkAllPagesAsync(IQueryable<TestEntity> source, CursorPaginationOptions opts)
    {
        var ids = new List<int>();
        var page = await source.ToCursorPagedAsync(opts);

        while (true)
        {
            ids.AddRange(page.Items.Select(e => e.Id));

            if (!page.HasNextPage || page.EndCursor == null)
                break;

            page = await source.ToCursorPagedAsync(opts with { After = page.EndCursor });
        }

        return ids;
    }

    private static int[] ExpectedScoreThenIdOrder(bool descending)
    {
        var rows = Enumerable.Range(1, RowCount).Select(i => new { Id = i, Score = ((i - 1) % 4) + 1 });

        var ordered = descending
            ? rows.OrderByDescending(r => r.Score).ThenByDescending(r => r.Id)
            : rows.OrderBy(r => r.Score).ThenBy(r => r.Id);

        return ordered.Select(r => r.Id).ToArray();
    }

    [Fact]
    public async Task CursorPagedAsync_DescendingSort_ReturnsTheNextDescendingPage()
    {
        using var context = CreateInMemoryContext();

        var opts = new CursorPaginationOptions { PageSize = 5, CursorProperty = "Id", SortBy = "Id", SortDescending = true };

        var firstPage = await context.Entities.AsQueryable().ToCursorPagedAsync(opts);
        var secondPage = await context.Entities.AsQueryable().ToCursorPagedAsync(opts with { After = firstPage.EndCursor });

        Assert.Equal(new[] { 20, 19, 18, 17, 16 }, firstPage.Items.Select(e => e.Id));
        Assert.Equal(new[] { 15, 14, 13, 12, 11 }, secondPage.Items.Select(e => e.Id));
    }

    [Fact]
    public async Task CursorPagedAsync_DescendingSort_WalksEveryRowExactlyOnce()
    {
        using var context = CreateInMemoryContext();

        var ids = await WalkAllPagesAsync(
            context.Entities.AsQueryable(),
            new CursorPaginationOptions { PageSize = 3, CursorProperty = "Id", SortBy = "Id", SortDescending = true });

        Assert.Equal(Enumerable.Range(1, RowCount).Reverse().ToArray(), ids);
    }

    [Fact]
    public async Task CursorPagedAsync_DescendingSortOnSqlite_ReturnsTheNextDescendingPage()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        using var context = await CreateSqliteContextAsync(connection);

        var opts = new CursorPaginationOptions { PageSize = 5, CursorProperty = "Id", SortBy = "Id", SortDescending = true };

        var firstPage = await context.Entities.AsQueryable().ToCursorPagedAsync(opts);
        var secondPage = await context.Entities.AsQueryable().ToCursorPagedAsync(opts with { After = firstPage.EndCursor });

        Assert.Equal(new[] { 20, 19, 18, 17, 16 }, firstPage.Items.Select(e => e.Id));
        Assert.Equal(new[] { 15, 14, 13, 12, 11 }, secondPage.Items.Select(e => e.Id));
    }

    [Fact]
    public async Task CursorPagedAsync_DescendingWithoutSortBy_PagesInDescendingOrder()
    {
        using var context = CreateInMemoryContext();

        var opts = new CursorPaginationOptions { PageSize = 5, CursorProperty = "Id", SortDescending = true };

        var firstPage = await context.Entities.AsQueryable().ToCursorPagedAsync(opts);
        var secondPage = await context.Entities.AsQueryable().ToCursorPagedAsync(opts with { After = firstPage.EndCursor });

        Assert.Equal(new[] { 20, 19, 18, 17, 16 }, firstPage.Items.Select(e => e.Id));
        Assert.Equal(new[] { 15, 14, 13, 12, 11 }, secondPage.Items.Select(e => e.Id));
    }

    [Fact]
    public async Task CursorPagedAsync_SortByOtherThanCursorProperty_SkipsNoRow()
    {
        using var context = CreateInMemoryContext();

        var ids = await WalkAllPagesAsync(
            context.Entities.AsQueryable(),
            new CursorPaginationOptions { PageSize = 3, CursorProperty = "Id", SortBy = "Score" });

        Assert.Equal(ExpectedScoreThenIdOrder(descending: false), ids);
    }

    [Fact]
    public async Task CursorPagedAsync_SortByOtherThanCursorProperty_SecondPageContinuesTheKeyset()
    {
        using var context = CreateInMemoryContext();

        var opts = new CursorPaginationOptions { PageSize = 3, CursorProperty = "Id", SortBy = "Score" };

        var firstPage = await context.Entities.AsQueryable().ToCursorPagedAsync(opts);
        var secondPage = await context.Entities.AsQueryable().ToCursorPagedAsync(opts with { After = firstPage.EndCursor });

        Assert.Equal(new[] { 1, 5, 9 }, firstPage.Items.Select(e => e.Id));
        Assert.Equal(new[] { 13, 17, 2 }, secondPage.Items.Select(e => e.Id));
    }

    [Fact]
    public async Task CursorPagedAsync_SortByOtherThanCursorPropertyOnSqlite_SkipsNoRow()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        using var context = await CreateSqliteContextAsync(connection);

        var ids = await WalkAllPagesAsync(
            context.Entities.AsQueryable(),
            new CursorPaginationOptions { PageSize = 3, CursorProperty = "Id", SortBy = "Score" });

        Assert.Equal(ExpectedScoreThenIdOrder(descending: false), ids);
    }

    [Fact]
    public async Task CursorPagedAsync_DescendingSortByOtherThanCursorProperty_SkipsNoRow()
    {
        using var context = CreateInMemoryContext();

        var ids = await WalkAllPagesAsync(
            context.Entities.AsQueryable(),
            new CursorPaginationOptions { PageSize = 3, CursorProperty = "Id", SortBy = "Score", SortDescending = true });

        Assert.Equal(ExpectedScoreThenIdOrder(descending: true), ids);
    }

    [Fact]
    public async Task CursorPagedAsync_DescendingSortByOtherThanCursorPropertyOnSqlite_SkipsNoRow()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        using var context = await CreateSqliteContextAsync(connection);

        var ids = await WalkAllPagesAsync(
            context.Entities.AsQueryable(),
            new CursorPaginationOptions { PageSize = 3, CursorProperty = "Id", SortBy = "Score", SortDescending = true });

        Assert.Equal(ExpectedScoreThenIdOrder(descending: true), ids);
    }

    [Fact]
    public async Task CursorPagedAsync_StringSortKeyWithIdTieBreaker_SkipsNoRow()
    {
        using var context = CreateInMemoryContext();

        var ids = await WalkAllPagesAsync(
            context.Entities.AsQueryable(),
            new CursorPaginationOptions { PageSize = 4, CursorProperty = "Id", SortBy = "Name" });

        Assert.Equal(Enumerable.Range(1, RowCount).ToArray(), ids);
    }

    [Fact]
    public async Task CursorPagedAsync_LegacySingleValueCursorWithSortBy_StillFiltersOnTheCursorProperty()
    {
        using var context = CreateInMemoryContext();

        var opts = new CursorPaginationOptions
        {
            PageSize = 3,
            CursorProperty = "Id",
            SortBy = "Score",
            After = QueryableHelper.EncodeCursor(9)
        };

        var page = await context.Entities.AsQueryable().ToCursorPagedAsync(opts);

        Assert.Equal(new[] { 13, 17, 10 }, page.Items.Select(e => e.Id));
    }

    [Fact]
    public async Task CursorPagedAsync_BeforeUnderDescendingSort_FiltersRowsAboveTheCursor()
    {
        using var context = CreateInMemoryContext();

        var opts = new CursorPaginationOptions
        {
            PageSize = 5,
            CursorProperty = "Id",
            SortBy = "Id",
            SortDescending = true,
            Before = QueryableHelper.EncodeCursor(16)
        };

        var page = await context.Entities.AsQueryable().ToCursorPagedAsync(opts);

        Assert.Equal(new[] { 20, 19, 18, 17 }, page.Items.Select(e => e.Id));
    }

    [Fact]
    public async Task BuildCursor_SortByOtherThanCursorProperty_CarriesBothValues()
    {
        var item = new TestEntity { Id = 17, Score = 1, Name = "Item17" };

        var composite = QueryableHelper.BuildCursor(
            item,
            new CursorPaginationOptions { CursorProperty = "Id", SortBy = "Score" });

        Assert.NotNull(composite);
        Assert.NotEqual(QueryableHelper.EncodeCursor(17), composite);
        Assert.Equal(17, QueryableHelper.DecodeCursor(composite!, typeof(int)));
    }

    [Fact]
    public void BuildCursor_SortByEqualsCursorProperty_ProducesTheSingleValueCursor()
    {
        var item = new TestEntity { Id = 17, Score = 1, Name = "Item17" };

        var cursor = QueryableHelper.BuildCursor(
            item,
            new CursorPaginationOptions { CursorProperty = "Id", SortBy = "id" });

        Assert.Equal(QueryableHelper.EncodeCursor(17), cursor);
    }

    [Fact]
    public async Task CursorPagedAsync_CompositeCursorFromStringSortKey_CarriesTheTieBreaker()
    {
        using var context = CreateInMemoryContext();

        var opts = new CursorPaginationOptions { PageSize = 5, CursorProperty = "Id", SortBy = "Name" };

        var firstPage = await context.Entities.AsQueryable().ToCursorPagedAsync(opts);

        Assert.Equal(1, QueryableHelper.DecodeCursor(firstPage.StartCursor!, typeof(int)));
        Assert.Equal(5, QueryableHelper.DecodeCursor(firstPage.EndCursor!, typeof(int)));

        var secondPage = await context.Entities.AsQueryable().ToCursorPagedAsync(opts with { After = firstPage.EndCursor });

        Assert.Equal(new[] { 6, 7, 8, 9, 10 }, secondPage.Items.Select(e => e.Id));
    }
}
