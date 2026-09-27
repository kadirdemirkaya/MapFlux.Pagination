using Microsoft.EntityFrameworkCore;

namespace MapFlux.Pagination.Benchmarks;

public class BenchmarkRow
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int Age { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }
}

public class BenchmarkContext : DbContext
{
    public DbSet<BenchmarkRow> Rows => Set<BenchmarkRow>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseInMemoryDatabase($"benchmarks-{Guid.NewGuid():N}");
}
