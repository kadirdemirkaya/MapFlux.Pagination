using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MapFlux;
using MapFlux.Pagination.Abstractions;
using MapFlux.Pagination.Core;
using MapFlux.Pagination.Extensions;
using MapFlux.Pagination.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MapFlux.Pagination.Tests;

public class ProjectedPagingTests
{
    private const int RowCount = 12;

    private class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public int Price { get; set; }
        public int Rank { get; set; }
        public string Notes { get; set; } = string.Empty;
    }

    private class ProductDto
    {
        public string Title { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public int Price { get; set; }
    }

    private class UnprojectableProductDto
    {
        public int Name { get; set; }
    }

    private class ProductProfile : Profile
    {
        public override void Configure(IMapperConfigurationExpression cfg)
        {
            cfg.CreateMap<Product, ProductDto>(opt => opt.ForMember(d => d.Title, m => m.MapFrom(s => s.Name)));
        }
    }

    private class UnprojectableProductProfile : Profile
    {
        public override void Configure(IMapperConfigurationExpression cfg)
        {
            cfg.CreateMap<Product, UnprojectableProductDto>(opt => { });
        }
    }

    private class MapOnlyMapper : IMapper
    {
        private readonly Mapper _inner = new();

        public TDestination Map<TSource, TDestination>(TSource source) => _inner.Map<TSource, TDestination>(source);

        public void CreateMap<TProfile>() where TProfile : Profile, new() => _inner.CreateMap<TProfile>();
    }

    private class CommandCaptureInterceptor : DbCommandInterceptor
    {
        private readonly List<string> _commands = new();

        public IReadOnlyList<string> Commands
        {
            get
            {
                lock (_commands)
                    return _commands.ToList();
            }
        }

        public void Clear()
        {
            lock (_commands)
                _commands.Clear();
        }

        private void Capture(DbCommand command)
        {
            lock (_commands)
                _commands.Add(command.CommandText);
        }

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            Capture(command);
            return base.ReaderExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken ct = default)
        {
            Capture(command);
            return base.ReaderExecutingAsync(command, eventData, result, ct);
        }

        public override InterceptionResult<object> ScalarExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
        {
            Capture(command);
            return base.ScalarExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken ct = default)
        {
            Capture(command);
            return base.ScalarExecutingAsync(command, eventData, result, ct);
        }
    }

    private class SqliteContext : DbContext
    {
        private readonly SqliteConnection _connection;
        private readonly CommandCaptureInterceptor _interceptor;

        public SqliteContext(SqliteConnection connection, CommandCaptureInterceptor interceptor)
        {
            _connection = connection;
            _interceptor = interceptor;
        }

        public DbSet<Product> Products => Set<Product>();

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlite(_connection).AddInterceptors(_interceptor);
    }

    private class InMemoryContext : DbContext
    {
        private readonly string _databaseName = "projected_" + Guid.NewGuid();

        public DbSet<Product> Products => Set<Product>();

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseInMemoryDatabase(_databaseName);
    }

    private sealed class SqliteFixture : IDisposable
    {
        public SqliteConnection Connection { get; } = new("DataSource=:memory:");
        public CommandCaptureInterceptor Interceptor { get; } = new();
        public SqliteContext Context { get; private set; } = null!;

        public static async Task<SqliteFixture> CreateAsync()
        {
            var fixture = new SqliteFixture();
            await fixture.Connection.OpenAsync();

            fixture.Context = new SqliteContext(fixture.Connection, fixture.Interceptor);
            await fixture.Context.Database.EnsureCreatedAsync();

            fixture.Context.AddRange(CreateProducts());
            await fixture.Context.SaveChangesAsync();
            fixture.Context.ChangeTracker.Clear();
            fixture.Interceptor.Clear();

            return fixture;
        }

        public void Dispose()
        {
            Context.Dispose();
            Connection.Dispose();
        }
    }

    private static List<Product> CreateProducts()
        => Enumerable.Range(1, RowCount)
            .Select(i => new Product
            {
                Id = i,
                Name = $"Item{i:D2}",
                Category = i % 3 == 0 ? "Tools" : "Garden",
                Price = i * 7 % 5,
                Rank = 100 - i,
                Notes = $"Long description {i}"
            })
            .ToList();

    private static Mapper CreateMapper()
    {
        var mapper = new Mapper();
        mapper.CreateMap<ProductProfile>();
        return mapper;
    }

    private static string[] Render(IEnumerable<ProductDto> items)
        => items.Select(d => $"{d.Title}|{d.Category}|{d.Price}").ToArray();

    private static string Describe<T>(IPagedResult<T> page)
        => $"{page.TotalCount}/{page.PageNumber}/{page.PageSize}/{page.TotalPages}/{page.HasPreviousPage}/{page.HasNextPage}";

    private static string Describe<T>(ICursorPagedResult<T> page)
        => $"{page.StartCursor}/{page.EndCursor}/{page.HasPreviousPage}/{page.HasNextPage}/{page.TotalCount}";

    private static PaginationOptions FilteredOptions(int pageNumber) => new()
    {
        PageNumber = pageNumber,
        PageSize = 3,
        Filters = new List<FilterCriteria>
        {
            new() { PropertyName = "Rank", Operator = FilterOperator.LessThanOrEqual, Value = 98 }
        },
        SearchTerm = "ITEM",
        SearchProperties = new List<string> { "Name" },
        SortCriterias = new List<SortCriteria>
        {
            new() { PropertyName = "Price", Descending = true },
            new() { PropertyName = "Rank" }
        }
    };

    private static async Task<List<ICursorPagedResult<ProductDto>>> WalkAsync(
        Func<CursorPaginationOptions, Task<ICursorPagedResult<ProductDto>>> read,
        CursorPaginationOptions first,
        bool backward)
    {
        var pages = new List<ICursorPagedResult<ProductDto>>();
        var opts = first;

        for (var i = 0; i < RowCount; i++)
        {
            var page = await read(opts);
            pages.Add(page);

            if (backward ? !page.HasPreviousPage : !page.HasNextPage)
                break;

            opts = backward
                ? first with { After = null, Before = page.StartCursor }
                : first with { After = page.EndCursor, Before = null };
        }

        return pages;
    }

    [Fact]
    public async Task ToProjectedPagedAsync_Sqlite_SelectsOnlyMappedColumnsInTwoCommands()
    {
        using var fixture = await SqliteFixture.CreateAsync();

        var page = await fixture.Context.Products.ToProjectedPagedAsync<Product, ProductDto>(
            CreateMapper(), new PaginationOptions { PageNumber = 1, PageSize = 4, SortBy = "Id" });

        var commands = fixture.Interceptor.Commands;
        Assert.Equal(2, commands.Count);
        Assert.Contains("COUNT(*)", commands[0]);

        var pageCommand = commands[1];
        Assert.Contains("\"Name\"", pageCommand);
        Assert.Contains("\"Category\"", pageCommand);
        Assert.Contains("\"Price\"", pageCommand);
        Assert.DoesNotContain("\"Notes\"", pageCommand);
        Assert.DoesNotContain("\"Rank\"", pageCommand);
        Assert.Equal(new[] { "Item01|Garden|2", "Item02|Garden|4", "Item03|Tools|1", "Item04|Garden|3" }, Render(page.Items));
        Assert.Equal(RowCount, page.TotalCount);

        fixture.Interceptor.Clear();
        await new PaginatedMapper<Product, ProductDto>(CreateMapper()).MapPagedAsync(
            fixture.Context.Products, new PaginationOptions { PageNumber = 1, PageSize = 4, SortBy = "Id" });

        Assert.Contains("\"Notes\"", fixture.Interceptor.Commands[1]);
    }

    [Fact]
    public async Task ToProjectedPagedAsync_Sqlite_PageCommandMatchesPipelineFollowedByProjection()
    {
        using var fixture = await SqliteFixture.CreateAsync();
        var mapper = CreateMapper();
        var opts = FilteredOptions(2);

        var expected = QueryableHelper.ApplyFullPipeline(fixture.Context.Products, opts)
            .Skip(opts.Skip)
            .Take(opts.Take)
            .ProjectMapped<Product, ProductDto>(mapper)
            .ToQueryString();

        fixture.Interceptor.Clear();
        await fixture.Context.Products.ToProjectedPagedAsync<Product, ProductDto>(mapper, opts);

        var expectedSelect = expected.Substring(expected.IndexOf("SELECT", StringComparison.Ordinal));
        Assert.Equal(expectedSelect, fixture.Interceptor.Commands[1]);
        Assert.DoesNotContain("\"Notes\"", expected);
    }

    [Fact]
    public async Task ToProjectedCursorPagedAsync_Sqlite_SelectsCursorKeysAndMappedColumnsOnly()
    {
        using var fixture = await SqliteFixture.CreateAsync();

        var page = await fixture.Context.Products.ToProjectedCursorPagedAsync<Product, ProductDto>(
            CreateMapper(), new CursorPaginationOptions { PageSize = 4, CursorProperty = "Id", SortBy = "Rank" });

        var commands = fixture.Interceptor.Commands;
        Assert.Equal(2, commands.Count);

        var pageCommand = commands[1];
        Assert.Contains("\"Id\"", pageCommand);
        Assert.Contains("\"Rank\"", pageCommand);
        Assert.Contains("\"Name\"", pageCommand);
        Assert.DoesNotContain("\"Notes\"", pageCommand);
        Assert.Equal(4, page.Items.Count);
        Assert.NotNull(page.EndCursor);
    }

    [Fact]
    public async Task ToProjectedCursorPagedAsync_Sqlite_IncludeTotalCountFalse_IssuesOnlyThePageCommand()
    {
        using var fixture = await SqliteFixture.CreateAsync();

        var page = await fixture.Context.Products.ToProjectedCursorPagedAsync<Product, ProductDto>(
            CreateMapper(), new CursorPaginationOptions { PageSize = 4, IncludeTotalCount = false });

        Assert.Single(fixture.Interceptor.Commands);
        Assert.Equal(-1, page.TotalCount);
        Assert.True(page.HasNextPage);
    }

    [Fact]
    public async Task ToProjectedPagedAsync_FilterSearchAndSortOnEntityProperties_MatchesInMemoryMapping()
    {
        using var fixture = await SqliteFixture.CreateAsync();
        var mapper = CreateMapper();

        for (var pageNumber = 1; pageNumber <= 4; pageNumber++)
        {
            var opts = FilteredOptions(pageNumber);

            var projected = await fixture.Context.Products.ToProjectedPagedAsync<Product, ProductDto>(mapper, opts);
            var mapped = await new PaginatedMapper<Product, ProductDto>(mapper).MapPagedAsync(fixture.Context.Products, opts);

            Assert.Equal(Render(mapped.Items), Render(projected.Items));
            Assert.Equal(Describe(mapped), Describe(projected));
        }
    }

    [Fact]
    public async Task ProjectCursorPagedAsync_ForwardWalkWithCompositeCursor_MatchesInMemoryMapping()
    {
        using var fixture = await SqliteFixture.CreateAsync();
        var paginatedMapper = new PaginatedMapper<Product, ProductDto>(CreateMapper());
        var first = new CursorPaginationOptions { PageSize = 5, CursorProperty = "Id", SortBy = "Price", SortDescending = true };

        var projected = await WalkAsync(o => paginatedMapper.ProjectCursorPagedAsync(fixture.Context.Products, o), first, backward: false);
        var mapped = await WalkAsync(o => paginatedMapper.MapCursorPagedAsync(fixture.Context.Products, o), first, backward: false);

        Assert.Equal(3, projected.Count);
        Assert.Equal(mapped.Select(p => Describe(p)), projected.Select(p => Describe(p)));
        Assert.Equal(mapped.SelectMany(p => Render(p.Items)), projected.SelectMany(p => Render(p.Items)));
        Assert.Equal(RowCount, projected.SelectMany(p => p.Items).Select(d => d.Title).Distinct().Count());
    }

    [Fact]
    public async Task ProjectCursorPagedAsync_BackwardWalk_MatchesInMemoryMapping()
    {
        using var fixture = await SqliteFixture.CreateAsync();
        var paginatedMapper = new PaginatedMapper<Product, ProductDto>(CreateMapper());
        var baseOptions = new CursorPaginationOptions { PageSize = 4, CursorProperty = "Id", SortBy = "Rank" };

        var last = (await WalkAsync(o => paginatedMapper.MapCursorPagedAsync(fixture.Context.Products, o), baseOptions, backward: false))[^1];
        var first = baseOptions with { Before = last.StartCursor };

        var projected = await WalkAsync(o => paginatedMapper.ProjectCursorPagedAsync(fixture.Context.Products, o), first, backward: true);
        var mapped = await WalkAsync(o => paginatedMapper.MapCursorPagedAsync(fixture.Context.Products, o), first, backward: true);

        Assert.Equal(2, projected.Count);
        Assert.Equal(mapped.Select(p => Describe(p)), projected.Select(p => Describe(p)));
        Assert.Equal(mapped.SelectMany(p => Render(p.Items)), projected.SelectMany(p => Render(p.Items)));
        Assert.False(projected[^1].HasPreviousPage);
    }

    [Fact]
    public async Task ToProjectedCursorPagedAsync_CursorsAreAcceptedByTheUnprojectedCursorPage()
    {
        using var fixture = await SqliteFixture.CreateAsync();
        var opts = new CursorPaginationOptions { PageSize = 5, CursorProperty = "Id", SortBy = "Price" };

        var projected = await fixture.Context.Products.ToProjectedCursorPagedAsync<Product, ProductDto>(CreateMapper(), opts);
        var entities = await fixture.Context.Products.ToCursorPagedAsync(opts);
        var nextEntities = await fixture.Context.Products.ToCursorPagedAsync(opts with { After = projected.EndCursor });

        Assert.Equal(entities.EndCursor, projected.EndCursor);
        Assert.Equal(entities.StartCursor, projected.StartCursor);
        Assert.Equal(5, nextEntities.Items.Count);
        Assert.Empty(entities.Items.Select(e => e.Id).Intersect(nextEntities.Items.Select(e => e.Id)));
    }

    [Fact]
    public async Task ToProjectedCursorPagedAsync_UnknownCursorPropertyWithoutStrictMode_ReturnsNullCursorsLikeTheMappedPage()
    {
        var source = CreateProducts().AsQueryable();
        var mapper = CreateMapper();
        var opts = new CursorPaginationOptions { PageSize = 5, CursorProperty = "Missing" };

        var projected = await source.ToProjectedCursorPagedAsync<Product, ProductDto>(mapper, opts);
        var mapped = await source.ToCursorPagedAsync<Product, ProductDto>(mapper, opts);

        Assert.Null(projected.StartCursor);
        Assert.Null(projected.EndCursor);
        Assert.Equal(Describe(mapped), Describe(projected));
        Assert.Equal(Render(mapped.Items), Render(projected.Items));
    }

    [Fact]
    public async Task ProjectPagedAsync_GlobalOptions_ClampPageSizeAndApplyDefaultOrdering()
    {
        using var fixture = await SqliteFixture.CreateAsync();
        var paginatedMapper = new PaginatedMapper<Product, ProductDto>(
            CreateMapper(),
            new PaginationGlobalOptions { MaxPageSize = 3, DefaultSortProperty = "Rank" });

        var page = await paginatedMapper.ProjectPagedAsync(fixture.Context.Products, new PaginationOptions { PageNumber = 1, PageSize = 50 });
        var cursorPage = await paginatedMapper.ProjectCursorPagedAsync(fixture.Context.Products, new CursorPaginationOptions { PageSize = 50 });

        Assert.Equal(3, page.PageSize);
        Assert.Equal(new[] { "Item12|Tools|4", "Item11|Garden|2", "Item10|Garden|0" }, Render(page.Items));
        Assert.Contains("ORDER BY", fixture.Interceptor.Commands[1]);
        Assert.Equal(3, cursorPage.Items.Count);
    }

    [Fact]
    public async Task ToProjectedPagedAsync_UnprojectableMap_ThrowsBeforeAnyCommand()
    {
        using var fixture = await SqliteFixture.CreateAsync();
        var mapper = new Mapper();
        mapper.CreateMap<UnprojectableProductProfile>();

        var offset = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Context.Products.ToProjectedPagedAsync<Product, UnprojectableProductDto>(mapper, new PaginationOptions()));
        var cursor = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Context.Products.ToProjectedCursorPagedAsync<Product, UnprojectableProductDto>(mapper, new CursorPaginationOptions()));

        Assert.Contains("UnprojectableProductDto.Name", offset.Message);
        Assert.Contains("UnprojectableProductDto.Name", cursor.Message);
        Assert.Empty(fixture.Interceptor.Commands);
    }

    [Fact]
    public async Task ProjectPagedAsync_MapperWithoutProjectionSupport_ThrowsClearError()
    {
        var mapper = new MapOnlyMapper();
        mapper.CreateMap<ProductProfile>();
        var paginatedMapper = new PaginatedMapper<Product, ProductDto>(mapper);
        var source = CreateProducts().AsQueryable();

        var offset = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            paginatedMapper.ProjectPagedAsync(source, new PaginationOptions()));
        var cursor = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            paginatedMapper.ProjectCursorPagedAsync(source, new CursorPaginationOptions()));
        var mapped = await paginatedMapper.MapPagedAsync(source, new PaginationOptions { PageSize = 2 });

        Assert.Contains(nameof(IProjectionMapper), offset.Message);
        Assert.Contains(nameof(MapOnlyMapper), offset.Message);
        Assert.Equal(offset.Message, cursor.Message);
        Assert.Equal(2, mapped.Items.Count);
    }

    [Fact]
    public async Task ToProjectedPagedAsync_NullArguments_Throw()
    {
        var source = CreateProducts().AsQueryable();
        var mapper = CreateMapper();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            source.ToProjectedPagedAsync<Product, ProductDto>(null!, new PaginationOptions()));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            source.ToProjectedPagedAsync<Product, ProductDto>(mapper, null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            source.ToProjectedCursorPagedAsync<Product, ProductDto>(null!, new CursorPaginationOptions()));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            source.ToProjectedCursorPagedAsync<Product, ProductDto>(mapper, new CursorPaginationOptions { PageSize = 0 }));
    }

    [Fact]
    public async Task AddMapFluxPagination_ResolvesProjectedPaginatedMapper()
    {
        var services = new ServiceCollection();
        services.AddMapFluxPagination(cfg => cfg.AddProfile<ProductProfile>(), opts => opts.MaxPageSize = 2);
        services.AddMapFluxPagination(cfg => cfg.AddProfile<UnprojectableProductProfile>());

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var projected = scope.ServiceProvider.GetRequiredService<IProjectedPaginatedMapper<Product, ProductDto>>();
        var mapped = scope.ServiceProvider.GetRequiredService<IPaginatedMapper<Product, ProductDto>>();
        var page = await projected.ProjectPagedAsync(CreateProducts().AsQueryable(), new PaginationOptions { PageSize = 10, SortBy = "Id" });

        Assert.IsType<PaginatedMapper<Product, ProductDto>>(projected);
        Assert.IsType<PaginatedMapper<Product, ProductDto>>(mapped);
        Assert.Equal(new[] { "Item01|Garden|2", "Item02|Garden|4" }, Render(page.Items));
        Assert.Single(services, d => d.ServiceType == typeof(IProjectedPaginatedMapper<,>));
    }

    [Fact]
    public async Task ProjectedPaging_EfInMemoryProvider_MatchesInMemoryMapping()
    {
        using var context = new InMemoryContext();
        context.AddRange(CreateProducts());
        await context.SaveChangesAsync();

        var paginatedMapper = new PaginatedMapper<Product, ProductDto>(CreateMapper());
        var opts = FilteredOptions(2);
        var cursorOpts = new CursorPaginationOptions { PageSize = 4, SortBy = "Price" };

        var projected = await paginatedMapper.ProjectPagedAsync(context.Products, opts);
        var mapped = await paginatedMapper.MapPagedAsync(context.Products, opts);
        var projectedCursor = await WalkAsync(o => paginatedMapper.ProjectCursorPagedAsync(context.Products, o), cursorOpts, backward: false);
        var mappedCursor = await WalkAsync(o => paginatedMapper.MapCursorPagedAsync(context.Products, o), cursorOpts, backward: false);

        Assert.Equal(Render(mapped.Items), Render(projected.Items));
        Assert.Equal(Describe(mapped), Describe(projected));
        Assert.Equal(mappedCursor.Select(p => Describe(p)), projectedCursor.Select(p => Describe(p)));
        Assert.Equal(mappedCursor.SelectMany(p => Render(p.Items)), projectedCursor.SelectMany(p => Render(p.Items)));
    }

    [Fact]
    public async Task ProjectedPaging_NonAsyncQueryable_MatchesInMemoryMapping()
    {
        var source = CreateProducts().AsQueryable();
        var mapper = CreateMapper();
        var opts = FilteredOptions(1);
        var cursorOpts = new CursorPaginationOptions { PageSize = 5, SortBy = "Rank", SortDescending = true };

        var projected = await source.ToProjectedPagedAsync<Product, ProductDto>(mapper, opts);
        var mapped = await source.ToPagedAsync<Product, ProductDto>(mapper, opts);
        var projectedCursor = await WalkAsync(o => source.ToProjectedCursorPagedAsync<Product, ProductDto>(mapper, o), cursorOpts, backward: false);
        var mappedCursor = await WalkAsync(o => source.ToCursorPagedAsync<Product, ProductDto>(mapper, o), cursorOpts, backward: false);

        Assert.Equal(Render(mapped.Items), Render(projected.Items));
        Assert.Equal(Describe(mapped), Describe(projected));
        Assert.Equal(mappedCursor.Select(p => Describe(p)), projectedCursor.Select(p => Describe(p)));
        Assert.Equal(mappedCursor.SelectMany(p => Render(p.Items)), projectedCursor.SelectMany(p => Render(p.Items)));
    }
}
