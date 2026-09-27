using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MapFlux.Pagination.Core;
using MapFlux.Pagination.Extensions;
using MapFlux.Pagination.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MapFlux.Pagination.Tests;

public class CursorFormatTests
{
    private class TestEntity
    {
        public int Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public decimal Price { get; set; }
        public double Weight { get; set; }
    }

    private class TestDbContext : DbContext
    {
        public DbSet<TestEntity> Entities => Set<TestEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseInMemoryDatabase("cursor_format_testdb_" + Guid.NewGuid());
    }

    private static TestDbContext CreateContext()
    {
        var context = new TestDbContext();

        for (var i = 1; i <= 20; i++)
        {
            context.Entities.Add(new TestEntity
            {
                Id = i,
                CreatedAt = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc).AddTicks(i * 1234),
                UpdatedAt = new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.FromHours(3)).AddTicks(i * 1234),
                Price = 5.5m + (i * 0.25m),
                Weight = 0.1 + (i * 0.0000001)
            });
        }

        context.SaveChanges();
        return context;
    }

    private static string LegacyCursor(object value)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(value.ToString()!));

    private static async Task RunUnderCultureAsync(CultureInfo culture, Func<Task> body)
    {
        var previousCulture = Thread.CurrentThread.CurrentCulture;
        var previousUiCulture = Thread.CurrentThread.CurrentUICulture;

        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;

        try
        {
            await body();
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previousCulture;
            Thread.CurrentThread.CurrentUICulture = previousUiCulture;
        }
    }

    [Fact]
    public async Task CursorPagedAsync_DateTimeCursor_ReturnsNextPageWithoutRepeatingRows()
    {
        using var context = CreateContext();

        var firstPage = await context.Entities.AsQueryable()
            .ToCursorPagedAsync(new CursorPaginationOptions { PageSize = 5, CursorProperty = "CreatedAt" });

        var secondPage = await context.Entities.AsQueryable()
            .ToCursorPagedAsync(new CursorPaginationOptions
            {
                PageSize = 5,
                CursorProperty = "CreatedAt",
                After = firstPage.EndCursor
            });

        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, firstPage.Items.Select(e => e.Id).ToArray());
        Assert.Equal(new[] { 6, 7, 8, 9, 10 }, secondPage.Items.Select(e => e.Id).ToArray());
    }

    [Fact]
    public async Task CursorPagedAsync_DateTimeOffsetCursor_ReturnsNextPageWithoutRepeatingRows()
    {
        using var context = CreateContext();

        var firstPage = await context.Entities.AsQueryable()
            .ToCursorPagedAsync(new CursorPaginationOptions { PageSize = 5, CursorProperty = "UpdatedAt" });

        var secondPage = await context.Entities.AsQueryable()
            .ToCursorPagedAsync(new CursorPaginationOptions
            {
                PageSize = 5,
                CursorProperty = "UpdatedAt",
                After = firstPage.EndCursor
            });

        Assert.Equal(new[] { 6, 7, 8, 9, 10 }, secondPage.Items.Select(e => e.Id).ToArray());
    }

    [Fact]
    public async Task CursorPagedAsync_CursorIssuedUnderTurkishCulture_DecodesUnderEnglishCulture()
    {
        using var context = CreateContext();

        string? endCursor = null;

        await RunUnderCultureAsync(CultureInfo.GetCultureInfo("tr-TR"), async () =>
        {
            var firstPage = await context.Entities.AsQueryable()
                .ToCursorPagedAsync(new CursorPaginationOptions { PageSize = 5, CursorProperty = "CreatedAt" });

            endCursor = firstPage.EndCursor;
        });

        Assert.NotNull(endCursor);

        await RunUnderCultureAsync(CultureInfo.GetCultureInfo("en-US"), async () =>
        {
            var secondPage = await context.Entities.AsQueryable()
                .ToCursorPagedAsync(new CursorPaginationOptions
                {
                    PageSize = 5,
                    CursorProperty = "CreatedAt",
                    After = endCursor
                });

            Assert.Equal(new[] { 6, 7, 8, 9, 10 }, secondPage.Items.Select(e => e.Id).ToArray());
        });
    }

    [Fact]
    public async Task CursorPagedAsync_DecimalCursorIssuedUnderTurkishCulture_ReturnsNextPage()
    {
        using var context = CreateContext();

        string? endCursor = null;

        await RunUnderCultureAsync(CultureInfo.GetCultureInfo("tr-TR"), async () =>
        {
            var firstPage = await context.Entities.AsQueryable()
                .ToCursorPagedAsync(new CursorPaginationOptions { PageSize = 5, CursorProperty = "Price" });

            Assert.Equal(new[] { 1, 2, 3, 4, 5 }, firstPage.Items.Select(e => e.Id).ToArray());
            endCursor = firstPage.EndCursor;
        });

        await RunUnderCultureAsync(CultureInfo.GetCultureInfo("en-US"), async () =>
        {
            var secondPage = await context.Entities.AsQueryable()
                .ToCursorPagedAsync(new CursorPaginationOptions
                {
                    PageSize = 5,
                    CursorProperty = "Price",
                    After = endCursor
                });

            Assert.Equal(new[] { 6, 7, 8, 9, 10 }, secondPage.Items.Select(e => e.Id).ToArray());
        });
    }

    [Fact]
    public async Task CursorPagedAsync_DoubleCursor_ReturnsNextPageWithoutLosingPrecision()
    {
        using var context = CreateContext();

        var firstPage = await context.Entities.AsQueryable()
            .ToCursorPagedAsync(new CursorPaginationOptions { PageSize = 5, CursorProperty = "Weight" });

        var secondPage = await context.Entities.AsQueryable()
            .ToCursorPagedAsync(new CursorPaginationOptions
            {
                PageSize = 5,
                CursorProperty = "Weight",
                After = firstPage.EndCursor
            });

        Assert.Equal(new[] { 6, 7, 8, 9, 10 }, secondPage.Items.Select(e => e.Id).ToArray());
    }

    [Fact]
    public async Task CursorPagedAsync_LegacyIntCursor_StillDecodes()
    {
        using var context = CreateContext();

        var secondPage = await context.Entities.AsQueryable()
            .ToCursorPagedAsync(new CursorPaginationOptions
            {
                PageSize = 5,
                CursorProperty = "Id",
                After = LegacyCursor(5)
            });

        Assert.Equal(new[] { 6, 7, 8, 9, 10 }, secondPage.Items.Select(e => e.Id).ToArray());
    }

    [Fact]
    public void DecodeCursor_LegacyDateTimeCursor_StillDecodesTheWayItWasWritten()
    {
        var value = new DateTime(2026, 1, 1, 10, 0, 0);
        var legacyText = value.ToString();

        var decoded = QueryableHelper.DecodeCursor(LegacyCursor(value), typeof(DateTime));

        Assert.Equal(DateTime.Parse(legacyText), decoded);
        Assert.Equal(value, decoded);
    }

    [Fact]
    public async Task CursorPagedAsync_LegacyDateTimeCursor_StillFiltersRows()
    {
        using var context = CreateContext();

        var boundary = new DateTime(2026, 1, 1, 10, 0, 1);

        var page = await context.Entities.AsQueryable()
            .ToCursorPagedAsync(new CursorPaginationOptions
            {
                PageSize = 5,
                CursorProperty = "CreatedAt",
                After = LegacyCursor(boundary)
            });

        Assert.Empty(page.Items);
    }

    [Fact]
    public void DecodeCursor_LegacyGuidCursor_StillDecodes()
    {
        var value = Guid.NewGuid();

        var decoded = QueryableHelper.DecodeCursor(LegacyCursor(value), typeof(Guid));

        Assert.Equal(value, decoded);
    }

    [Fact]
    public void DecodeCursor_LegacyLongCursor_StillDecodes()
    {
        var decoded = QueryableHelper.DecodeCursor(LegacyCursor(42L), typeof(long));

        Assert.Equal(42L, decoded);
    }

    [Fact]
    public void EncodeCursor_IsBase64AndRoundTripsDateTimeLosslessly()
    {
        var value = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc).AddTicks(1234567);

        var cursor = QueryableHelper.EncodeCursor(value);

        Assert.Equal(cursor, Convert.ToBase64String(Convert.FromBase64String(cursor)));
        Assert.Equal(value, QueryableHelper.DecodeCursor(cursor, typeof(DateTime)));
    }

    [Theory]
    [InlineData("tr-TR")]
    [InlineData("en-US")]
    [InlineData("de-DE")]
    public void EncodeCursor_ProducesTheSameCursorInEveryCulture(string cultureName)
    {
        var previousCulture = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);

        try
        {
            var payload = Encoding.UTF8.GetString(Convert.FromBase64String(QueryableHelper.EncodeCursor(5.5m)));

            Assert.EndsWith("5.5", payload, StringComparison.Ordinal);
            Assert.Equal(5.5m, QueryableHelper.DecodeCursor(QueryableHelper.EncodeCursor(5.5m), typeof(decimal)));
            Assert.Equal(5.5d, QueryableHelper.DecodeCursor(QueryableHelper.EncodeCursor(5.5d), typeof(double)));
            Assert.Equal(
                new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc).AddTicks(7),
                QueryableHelper.DecodeCursor(
                    QueryableHelper.EncodeCursor(new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc).AddTicks(7)),
                    typeof(DateTime)));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previousCulture;
        }
    }

    [Fact]
    public void DecodeCursor_MalformedCursor_ReturnsNull()
    {
        Assert.Null(QueryableHelper.DecodeCursor("not-base64!!", typeof(int)));
        Assert.Null(QueryableHelper.DecodeCursor(LegacyCursor("abc"), typeof(int)));
        Assert.Null(QueryableHelper.DecodeCursor(QueryableHelper.EncodeCursor("abc"), typeof(int)));
    }
}
