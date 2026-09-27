using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using MapFlux;
using MapFlux.Pagination.Abstractions;
using MapFlux.Pagination.Core;
using MapFlux.Pagination.Exceptions;
using MapFlux.Pagination.Extensions;
using MapFlux.Pagination.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace MapFlux.Pagination.Tests;

public class DefaultOrderingTests
{
    private class TestEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private class TestEntityDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private class Document
    {
        [Key]
        public string Code { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
    }

    private class DocumentDto
    {
        public string Code { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
    }

    private class OrderingProfile : Profile
    {
        public override void Configure(IMapperConfigurationExpression cfg)
        {
            cfg.CreateMap<TestEntity, TestEntityDto>(opt => { });
            cfg.CreateMap<Document, DocumentDto>(opt => { });
        }
    }

    private class Keyless
    {
        public string Label { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    private class TestDbContext : DbContext
    {
        private readonly SqliteConnection _connection;

        public TestDbContext(SqliteConnection connection)
        {
            _connection = connection;
        }

        public DbSet<TestEntity> Entities => Set<TestEntity>();

        public DbSet<Document> Documents => Set<Document>();

        public List<string> ExecutedCommands { get; } = new();

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options
                .UseSqlite(_connection)
                .LogTo(ExecutedCommands.Add, new[] { DbLoggerCategory.Database.Command.Name }, LogLevel.Information);
    }

    private static string SelectedPageSql(TestDbContext context)
        => Assert.Single(context.ExecutedCommands.Where(command => command.Contains("LIMIT")));

    private static async Task<TestDbContext> CreateSeededContextAsync(SqliteConnection connection)
    {
        await connection.OpenAsync();

        var context = new TestDbContext(connection);
        await context.Database.EnsureCreatedAsync();

        for (var i = 1; i <= 5; i++)
            context.Entities.Add(new TestEntity { Id = i, Name = $"Name{6 - i}" });

        foreach (var code in new[] { "E", "D", "C", "B", "A" })
            context.Documents.Add(new Document { Code = code, Title = $"Title{code}" });

        await context.SaveChangesAsync();
        context.ExecutedCommands.Clear();

        return context;
    }

    private static string OrderByClause(string sql)
    {
        var index = sql.IndexOf("ORDER BY");
        return index < 0 ? string.Empty : sql.Substring(index);
    }

    private static string PagedSql<T>(IQueryable<T> source, PaginationOptions opts)
        => QueryableHelper.ApplyFullPipeline(source, opts).Skip(opts.Skip).Take(opts.Take).ToQueryString();

    private static PaginationOptions FirstTwo => new() { PageNumber = 1, PageSize = 2 };

    [Fact]
    public async Task ApplyFullPipeline_WithoutOptIn_ShouldNotEmitOrderBy()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        using var context = await CreateSeededContextAsync(connection);

        var sql = PagedSql(context.Entities, FirstTwo);

        Assert.DoesNotContain("ORDER BY", sql);
    }

    [Fact]
    public async Task ApplyFullPipeline_WithDefaultSortProperty_ShouldEmitOrderBy()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        using var context = await CreateSeededContextAsync(connection);

        var opts = FirstTwo with { DefaultSortProperty = "Name" };

        Assert.Contains("\"Name\"", OrderByClause(PagedSql(context.Entities, opts)));

        var page = await context.Entities.AsQueryable().ToPagedAsync(opts);

        Assert.Equal(new[] { "Name1", "Name2" }, page.Items.Select(e => e.Name));
        Assert.Equal(5, page.TotalCount);
    }

    [Fact]
    public async Task ApplyFullPipeline_WithEnsureDeterministicOrder_ShouldOrderByConventionalKey()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        using var context = await CreateSeededContextAsync(connection);

        var opts = FirstTwo with { EnsureDeterministicOrder = true };

        Assert.Contains("\"Id\"", OrderByClause(PagedSql(context.Entities, opts)));

        var page = await context.Entities.AsQueryable().ToPagedAsync(opts);

        Assert.Equal(new[] { 1, 2 }, page.Items.Select(e => e.Id));
    }

    [Fact]
    public async Task ApplyFullPipeline_WithEnsureDeterministicOrder_ShouldOrderByAnnotatedKey()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        using var context = await CreateSeededContextAsync(connection);

        var opts = FirstTwo with { EnsureDeterministicOrder = true };

        Assert.Contains("\"Code\"", OrderByClause(PagedSql(context.Documents, opts)));

        var page = await context.Documents.AsQueryable().ToPagedAsync(opts);

        Assert.Equal(new[] { "A", "B" }, page.Items.Select(d => d.Code));
    }

    [Fact]
    public async Task ApplyFullPipeline_WithExplicitSortBy_ShouldNotAddDefaultOrdering()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        using var context = await CreateSeededContextAsync(connection);

        var opts = FirstTwo with
        {
            SortBy = "Name",
            SortDescending = true,
            EnsureDeterministicOrder = true,
            DefaultSortProperty = "Id"
        };

        var orderBy = OrderByClause(PagedSql(context.Entities, opts));

        Assert.Contains("\"Name\" DESC", orderBy);
        Assert.DoesNotContain("\"Id\"", orderBy);

        var page = await context.Entities.AsQueryable().ToPagedAsync(opts);

        Assert.Equal(new[] { "Name5", "Name4" }, page.Items.Select(e => e.Name));
    }

    [Fact]
    public async Task ApplyFullPipeline_WithOnlyUnknownSortCriteria_ShouldFallBackToDefaultOrdering()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        using var context = await CreateSeededContextAsync(connection);

        var opts = FirstTwo with
        {
            SortCriterias = new List<SortCriteria> { new() { PropertyName = "Missing" } },
            DefaultSortProperty = "Name"
        };

        Assert.Contains("\"Name\"", OrderByClause(PagedSql(context.Entities, opts)));
    }

    [Fact]
    public async Task ApplyFullPipeline_WithKnownSortCriteria_ShouldNotAddDefaultOrdering()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        using var context = await CreateSeededContextAsync(connection);

        var opts = FirstTwo with
        {
            SortCriterias = new List<SortCriteria> { new() { PropertyName = "Name", Descending = true } },
            EnsureDeterministicOrder = true
        };

        var orderBy = OrderByClause(PagedSql(context.Entities, opts));

        Assert.Contains("\"Name\" DESC", orderBy);
        Assert.DoesNotContain("\"Id\"", orderBy);
    }

    [Fact]
    public void ApplyFullPipeline_WithUnknownDefaultSortProperty_ShouldLeaveQueryUnordered()
    {
        var query = QueryableHelper.ApplyFullPipeline(
            new List<TestEntity>().AsQueryable(),
            new PaginationOptions { DefaultSortProperty = "Missing" });

        Assert.DoesNotContain("OrderBy", query.Expression.ToString());
    }

    [Fact]
    public void ApplyFullPipeline_WithUnknownDefaultSortPropertyAndStrictMode_ShouldThrow()
    {
        var exception = Assert.Throws<PaginationStrictModeException>(() =>
            QueryableHelper.ApplyFullPipeline(
                new List<TestEntity>().AsQueryable(),
                new PaginationOptions { DefaultSortProperty = "Missing", StrictMode = true }));

        Assert.Equal("Missing", exception.PropertyName);
    }

    [Fact]
    public void ApplyFullPipeline_WithUnknownDefaultSortProperty_ShouldFallBackToKeyWhenRequested()
    {
        var query = QueryableHelper.ApplyFullPipeline(
            new List<TestEntity>().AsQueryable(),
            new PaginationOptions { DefaultSortProperty = "Missing", EnsureDeterministicOrder = true });

        Assert.Contains("OrderBy(p => p.Id)", query.Expression.ToString());
    }

    [Fact]
    public void ApplyFullPipeline_WithoutResolvableKey_ShouldLeaveQueryUnordered()
    {
        var query = QueryableHelper.ApplyFullPipeline(
            new List<Keyless>().AsQueryable(),
            new PaginationOptions { EnsureDeterministicOrder = true });

        Assert.DoesNotContain("OrderBy", query.Expression.ToString());
    }

    [Fact]
    public void ApplyFullPipeline_WithoutResolvableKeyAndStrictMode_ShouldThrow()
    {
        Assert.Throws<PaginationStrictModeException>(() =>
            QueryableHelper.ApplyFullPipeline(
                new List<Keyless>().AsQueryable(),
                new PaginationOptions { EnsureDeterministicOrder = true, StrictMode = true }));
    }

    [Fact]
    public async Task MapPagedAsync_WithGlobalDefaultSortProperty_ShouldOrderThePage()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        using var context = await CreateSeededContextAsync(connection);

        var services = new ServiceCollection();
        services.AddMapFluxPagination(
            cfg => cfg.AddProfile<OrderingProfile>(),
            opts => opts.DefaultSortProperty = "Name");

        var provider = services.BuildServiceProvider();
        var mapper = provider.GetRequiredService<IPaginatedMapper<TestEntity, TestEntityDto>>();

        var page = await mapper.MapPagedAsync(context.Entities, FirstTwo);

        Assert.Equal(new[] { "Name1", "Name2" }, page.Items.Select(e => e.Name));
    }

    [Fact]
    public async Task MapPagedAsync_WithGlobalEnsureDeterministicOrder_ShouldOrderThePage()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        using var context = await CreateSeededContextAsync(connection);

        var services = new ServiceCollection();
        services.AddMapFluxPagination(
            cfg => cfg.AddProfile<OrderingProfile>(),
            opts => opts.EnsureDeterministicOrder = true);

        var provider = services.BuildServiceProvider();
        var mapper = provider.GetRequiredService<IPaginatedMapper<Document, DocumentDto>>();

        var page = await mapper.MapPagedAsync(context.Documents, FirstTwo);

        Assert.Contains("\"Code\"", OrderByClause(SelectedPageSql(context)));
        Assert.Equal(new[] { "A", "B" }, page.Items.Select(d => d.Code));
    }

    [Fact]
    public async Task MapPagedAsync_WithoutGlobalOptIn_ShouldNotEmitOrderBy()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        using var context = await CreateSeededContextAsync(connection);

        var services = new ServiceCollection();
        services.AddMapFluxPagination(cfg => cfg.AddProfile<OrderingProfile>());

        var provider = services.BuildServiceProvider();
        var mapper = provider.GetRequiredService<IPaginatedMapper<Document, DocumentDto>>();

        var page = await mapper.MapPagedAsync(context.Documents, FirstTwo);

        Assert.DoesNotContain("ORDER BY", SelectedPageSql(context));
        Assert.Equal(2, page.Items.Count);
        Assert.Equal(5, page.TotalCount);
    }

    [Fact]
    public async Task MapPagedAsync_WithRequestDefaultSortProperty_ShouldWinOverGlobalOption()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        using var context = await CreateSeededContextAsync(connection);

        var services = new ServiceCollection();
        services.AddMapFluxPagination(
            cfg => cfg.AddProfile<OrderingProfile>(),
            opts => opts.DefaultSortProperty = "Id");

        var provider = services.BuildServiceProvider();
        var mapper = provider.GetRequiredService<IPaginatedMapper<TestEntity, TestEntityDto>>();

        var page = await mapper.MapPagedAsync(context.Entities, FirstTwo with { DefaultSortProperty = "Name" });

        Assert.Equal(new[] { "Name1", "Name2" }, page.Items.Select(e => e.Name));
    }
}
