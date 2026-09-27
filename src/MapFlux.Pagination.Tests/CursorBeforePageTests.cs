using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MapFlux.Pagination.Core;
using MapFlux.Pagination.Exceptions;
using MapFlux.Pagination.Extensions;
using MapFlux.Pagination.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MapFlux.Pagination.Tests;

public class CursorBeforePageTests
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
            => options.UseInMemoryDatabase("cursor_before_" + Guid.NewGuid());
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

    [Fact]
    public async Task CursorPagedAsync_Before_ReturnsThePagePrecedingTheCursor()
    {
        using var context = CreateInMemoryContext();

        var page = await context.Entities.AsQueryable().ToCursorPagedAsync(new CursorPaginationOptions
        {
            PageSize = 5,
            CursorProperty = "Id",
            Before = QueryableHelper.EncodeCursor(16)
        });

        Assert.Equal(new[] { 11, 12, 13, 14, 15 }, page.Items.Select(e => e.Id));
        Assert.True(page.HasPreviousPage);
        Assert.True(page.HasNextPage);
        Assert.Equal(QueryableHelper.EncodeCursor(11), page.StartCursor);
        Assert.Equal(QueryableHelper.EncodeCursor(15), page.EndCursor);
    }

    [Fact]
    public async Task CursorPagedAsync_BeforeOnRelationalProvider_ReturnsThePagePrecedingTheCursor()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        using var context = await CreateSqliteContextAsync(connection);

        var page = await context.Entities.AsQueryable().ToCursorPagedAsync(new CursorPaginationOptions
        {
            PageSize = 5,
            CursorProperty = "Id",
            Before = QueryableHelper.EncodeCursor(16)
        });

        Assert.Equal(new[] { 11, 12, 13, 14, 15 }, page.Items.Select(e => e.Id));
        Assert.True(page.HasPreviousPage);
    }

    [Fact]
    public async Task CursorPagedAsync_BeforeWithFewerRowsAhead_ReportsNoPreviousPage()
    {
        using var context = CreateInMemoryContext();

        var page = await context.Entities.AsQueryable().ToCursorPagedAsync(new CursorPaginationOptions
        {
            PageSize = 5,
            CursorProperty = "Id",
            Before = QueryableHelper.EncodeCursor(4)
        });

        Assert.Equal(new[] { 1, 2, 3 }, page.Items.Select(e => e.Id));
        Assert.False(page.HasPreviousPage);
        Assert.True(page.HasNextPage);
    }

    [Fact]
    public async Task CursorPagedAsync_BeforeUnderDescendingSort_ReturnsThePrecedingPageInPageOrder()
    {
        using var context = CreateInMemoryContext();

        var page = await context.Entities.AsQueryable().ToCursorPagedAsync(new CursorPaginationOptions
        {
            PageSize = 5,
            CursorProperty = "Id",
            SortBy = "Id",
            SortDescending = true,
            Before = QueryableHelper.EncodeCursor(5)
        });

        Assert.Equal(new[] { 10, 9, 8, 7, 6 }, page.Items.Select(e => e.Id));
        Assert.True(page.HasPreviousPage);
        Assert.True(page.HasNextPage);
    }

    [Fact]
    public async Task CursorPagedAsync_BeforeWithSeparateSortKey_ContinuesTheCompositeKeysetBackwards()
    {
        using var context = CreateInMemoryContext();

        var opts = new CursorPaginationOptions
        {
            PageSize = 3,
            CursorProperty = "Id",
            SortBy = "Score"
        };

        var cursor = QueryableHelper.BuildCursor(new TestEntity { Id = 2, Score = 2 }, opts);

        var page = await context.Entities.AsQueryable().ToCursorPagedAsync(opts with { Before = cursor });

        Assert.Equal(new[] { 9, 13, 17 }, page.Items.Select(e => e.Id));
        Assert.True(page.HasPreviousPage);
        Assert.True(page.HasNextPage);
    }

    [Fact]
    public async Task CursorPagedAsync_BeforeTheStartCursorOfAPage_ReturnsThePageBefore()
    {
        using var context = CreateInMemoryContext();

        var opts = new CursorPaginationOptions { PageSize = 5, CursorProperty = "Id" };

        var firstPage = await context.Entities.AsQueryable().ToCursorPagedAsync(opts);
        var secondPage = await context.Entities.AsQueryable()
            .ToCursorPagedAsync(opts with { After = firstPage.EndCursor });
        var backToFirst = await context.Entities.AsQueryable()
            .ToCursorPagedAsync(opts with { Before = secondPage.StartCursor });

        Assert.Equal(firstPage.Items.Select(e => e.Id), backToFirst.Items.Select(e => e.Id));
        Assert.Equal(firstPage.StartCursor, backToFirst.StartCursor);
        Assert.Equal(firstPage.EndCursor, backToFirst.EndCursor);
        Assert.False(backToFirst.HasPreviousPage);
    }

    [Fact]
    public async Task CursorPagedAsync_LastPageWithAfter_KeepsReportingNoNextPage()
    {
        using var context = CreateInMemoryContext();

        var page = await context.Entities.AsQueryable().ToCursorPagedAsync(new CursorPaginationOptions
        {
            PageSize = 5,
            CursorProperty = "Id",
            After = QueryableHelper.EncodeCursor(15)
        });

        Assert.Equal(new[] { 16, 17, 18, 19, 20 }, page.Items.Select(e => e.Id));
        Assert.False(page.HasNextPage);
        Assert.True(page.HasPreviousPage);
    }

    [Fact]
    public async Task CursorPagedAsync_UndecodableAfterCursor_KeepsTheFirstPageAndReportsNoPreviousPage()
    {
        using var context = CreateInMemoryContext();

        var page = await context.Entities.AsQueryable().ToCursorPagedAsync(new CursorPaginationOptions
        {
            PageSize = 5,
            CursorProperty = "Id",
            After = "not-a-cursor!!"
        });

        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, page.Items.Select(e => e.Id));
        Assert.False(page.HasPreviousPage);
        Assert.True(page.HasNextPage);
    }

    [Fact]
    public async Task CursorPagedAsync_UndecodableBeforeCursor_KeepsTheFirstPageAndReportsNoPreviousPage()
    {
        using var context = CreateInMemoryContext();

        var page = await context.Entities.AsQueryable().ToCursorPagedAsync(new CursorPaginationOptions
        {
            PageSize = 5,
            CursorProperty = "Id",
            Before = "not-a-cursor!!"
        });

        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, page.Items.Select(e => e.Id));
        Assert.False(page.HasPreviousPage);
        Assert.True(page.HasNextPage);
    }

    [Fact]
    public async Task CursorPagedAsync_UnknownCursorProperty_KeepsTheFirstPageAndReportsNoPreviousPage()
    {
        using var context = CreateInMemoryContext();

        var page = await context.Entities.AsQueryable().ToCursorPagedAsync(new CursorPaginationOptions
        {
            PageSize = 5,
            CursorProperty = "Idd",
            After = QueryableHelper.EncodeCursor(5)
        });

        Assert.Equal(5, page.Items.Count);
        Assert.Null(page.StartCursor);
        Assert.Null(page.EndCursor);
        Assert.False(page.HasPreviousPage);
    }

    [Fact]
    public async Task CursorPagedAsync_UndecodableCursorInStrictMode_ThrowsWithTheCursorAndTargetType()
    {
        using var context = CreateInMemoryContext();

        var ex = await Assert.ThrowsAsync<PaginationStrictModeException>(
            () => context.Entities.AsQueryable().ToCursorPagedAsync(new CursorPaginationOptions
            {
                PageSize = 5,
                CursorProperty = "Id",
                After = "not-a-cursor!!",
                StrictMode = true
            }));

        Assert.Equal("Id", ex.PropertyName);
        Assert.Equal("not-a-cursor!!", ex.Value);
        Assert.Equal(typeof(int), ex.TargetType);
        Assert.Contains("not-a-cursor!!", ex.Message);
    }

    [Fact]
    public async Task CursorPagedAsync_UndecodableBeforeCursorInStrictMode_Throws()
    {
        using var context = CreateInMemoryContext();

        var ex = await Assert.ThrowsAsync<PaginationStrictModeException>(
            () => context.Entities.AsQueryable().ToCursorPagedAsync(new CursorPaginationOptions
            {
                PageSize = 5,
                CursorProperty = "Id",
                Before = "not-a-cursor!!",
                StrictMode = true
            }));

        Assert.Equal("Id", ex.PropertyName);
    }

    [Fact]
    public async Task CursorPagedAsync_UnknownCursorPropertyInStrictMode_ThrowsWithThePropertyName()
    {
        using var context = CreateInMemoryContext();

        var ex = await Assert.ThrowsAsync<PaginationStrictModeException>(
            () => context.Entities.AsQueryable().ToCursorPagedAsync(new CursorPaginationOptions
            {
                PageSize = 5,
                CursorProperty = "Idd",
                StrictMode = true
            }));

        Assert.Equal("Idd", ex.PropertyName);
        Assert.Equal(typeof(TestEntity), ex.TargetType);
        Assert.Contains("Idd", ex.Message);
    }

    [Fact]
    public async Task CursorPagedAsync_StrictModeWithAValidCursor_StillReturnsThePage()
    {
        using var context = CreateInMemoryContext();

        var page = await context.Entities.AsQueryable().ToCursorPagedAsync(new CursorPaginationOptions
        {
            PageSize = 5,
            CursorProperty = "Id",
            Before = QueryableHelper.EncodeCursor(16),
            StrictMode = true
        });

        Assert.Equal(new[] { 11, 12, 13, 14, 15 }, page.Items.Select(e => e.Id));
    }

    [Fact]
    public async Task CursorPagedAsync_WalkingBackwardsFromTheLastPage_VisitsEveryRowOnce()
    {
        using var context = CreateInMemoryContext();

        var opts = new CursorPaginationOptions { PageSize = 5, CursorProperty = "Id" };
        var page = await context.Entities.AsQueryable()
            .ToCursorPagedAsync(opts with { After = QueryableHelper.EncodeCursor(15) });

        var ids = new List<int>(page.Items.Select(e => e.Id));

        while (page.HasPreviousPage && page.StartCursor != null)
        {
            page = await context.Entities.AsQueryable()
                .ToCursorPagedAsync(opts with { Before = page.StartCursor });

            ids.InsertRange(0, page.Items.Select(e => e.Id));
        }

        Assert.Equal(Enumerable.Range(1, RowCount), ids);
    }
}
