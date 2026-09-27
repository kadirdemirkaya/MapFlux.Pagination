using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MapFlux.Pagination.Extensions;
using MapFlux.Pagination.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MapFlux.Pagination.Tests;

public class SearchCultureTests
{
    private class TestEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private class TestDbContext : DbContext
    {
        private readonly SqliteConnection _connection;

        public TestDbContext(SqliteConnection connection)
        {
            _connection = connection;
        }

        public DbSet<TestEntity> Entities => Set<TestEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlite(_connection);
    }

    [Fact]
    public async Task ApplySearch_TurkishCulture_MatchesInvariantly()
    {
        var previousCulture = Thread.CurrentThread.CurrentCulture;
        var previousUiCulture = Thread.CurrentThread.CurrentUICulture;

        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        try
        {
            using var context = new TestDbContext(connection);
            await context.Database.EnsureCreatedAsync();

            for (var i = 1; i <= 10; i++)
                context.Entities.Add(new TestEntity { Id = i, Name = $"Idem{i}" });

            for (var i = 1; i <= 10; i++)
                context.Entities.Add(new TestEntity { Id = 10 + i, Name = $"Other{i}" });

            await context.SaveChangesAsync();

            var turkish = CultureInfo.GetCultureInfo("tr-TR");
            Thread.CurrentThread.CurrentCulture = turkish;
            Thread.CurrentThread.CurrentUICulture = turkish;

            var opts = new PaginationOptions { PageNumber = 1, PageSize = 20, SearchTerm = "IDEM" };

            var result = await context.Entities.AsQueryable().ToPagedAsync(opts);

            Assert.Equal(10, result.TotalCount);
            Assert.All(result.Items, item => Assert.StartsWith("Idem", item.Name));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previousCulture;
            Thread.CurrentThread.CurrentUICulture = previousUiCulture;
        }
    }
}
