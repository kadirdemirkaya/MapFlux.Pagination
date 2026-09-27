using System;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MapFlux.Pagination.Extensions;
using MapFlux.Pagination.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace MapFlux.Pagination.Tests;

public class CursorTotalCountOptOutTests
{
    private const int RowCount = 20;

    private class TestEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private class CommandCountInterceptor : DbCommandInterceptor
    {
        public int CommandCount;

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            Interlocked.Increment(ref CommandCount);
            return base.ReaderExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken ct = default)
        {
            Interlocked.Increment(ref CommandCount);
            return base.ReaderExecutingAsync(command, eventData, result, ct);
        }

        public override InterceptionResult<object> ScalarExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
        {
            Interlocked.Increment(ref CommandCount);
            return base.ScalarExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken ct = default)
        {
            Interlocked.Increment(ref CommandCount);
            return base.ScalarExecutingAsync(command, eventData, result, ct);
        }
    }

    private class SqliteContext : DbContext
    {
        private readonly SqliteConnection _connection;
        private readonly CommandCountInterceptor _interceptor;

        public SqliteContext(SqliteConnection connection, CommandCountInterceptor interceptor)
        {
            _connection = connection;
            _interceptor = interceptor;
        }

        public DbSet<TestEntity> Entities => Set<TestEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlite(_connection).AddInterceptors(_interceptor);
    }

    private static async Task<SqliteContext> CreateSqliteContextAsync(SqliteConnection connection, CommandCountInterceptor interceptor)
    {
        await connection.OpenAsync();

        var context = new SqliteContext(connection, interceptor);
        await context.Database.EnsureCreatedAsync();

        for (var i = 1; i <= RowCount; i++)
            context.Add(new TestEntity { Id = i, Name = $"Item{i:D2}" });

        await context.SaveChangesAsync();

        return context;
    }

    [Fact]
    public async Task ToCursorPagedAsync_DefaultIncludesTotalCount_IssuesCountAndPageCommands()
    {
        var interceptor = new CommandCountInterceptor();
        using var connection = new SqliteConnection("DataSource=:memory:");
        using var context = await CreateSqliteContextAsync(connection, interceptor);

        interceptor.CommandCount = 0;

        var page = await context.Entities.AsQueryable().ToCursorPagedAsync(new CursorPaginationOptions
        {
            PageSize = 5,
            CursorProperty = "Id"
        });

        Assert.Equal(20, page.TotalCount);
        Assert.Equal(2, interceptor.CommandCount);
    }

    [Fact]
    public async Task ToCursorPagedAsync_IncludeTotalCountFalse_IssuesOnlyThePageCommand()
    {
        var interceptor = new CommandCountInterceptor();
        using var connection = new SqliteConnection("DataSource=:memory:");
        using var context = await CreateSqliteContextAsync(connection, interceptor);

        interceptor.CommandCount = 0;

        var page = await context.Entities.AsQueryable().ToCursorPagedAsync(new CursorPaginationOptions
        {
            PageSize = 5,
            CursorProperty = "Id",
            IncludeTotalCount = false
        });

        Assert.Equal(5, page.Items.Count);
        Assert.Equal(-1, page.TotalCount);
        Assert.Equal(1, interceptor.CommandCount);
    }

    [Fact]
    public async Task ToCursorPagedAsync_IncludeTotalCountFalse_StillReportsPagingFlagsCorrectly()
    {
        var interceptor = new CommandCountInterceptor();
        using var connection = new SqliteConnection("DataSource=:memory:");
        using var context = await CreateSqliteContextAsync(connection, interceptor);

        var page = await context.Entities.AsQueryable().ToCursorPagedAsync(new CursorPaginationOptions
        {
            PageSize = 5,
            CursorProperty = "Id",
            IncludeTotalCount = false
        });

        Assert.True(page.HasNextPage);
        Assert.False(page.HasPreviousPage);
    }
}
