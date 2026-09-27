using System.Diagnostics;
using MapFlux.Pagination.Core;
using MapFlux.Pagination.Models;

namespace MapFlux.Pagination.Benchmarks;

public sealed class PipelineBenchmark
{
    private const int WarmupOperations = 2_000;

    private const int MeasuredOperations = 20_000;

    private static int _sink;

    private readonly BenchmarkContext _context = new();

    private readonly PaginationOptions _options = new()
    {
        PageNumber = 1,
        PageSize = 20,
        Filters = new List<FilterCriteria>
        {
            new() { PropertyName = "isactive", Operator = FilterOperator.Equals, Value = true },
            new() { PropertyName = "age", Operator = FilterOperator.GreaterThanOrEqual, Value = 25 },
            new() { PropertyName = "name", Operator = FilterOperator.Contains, Value = "a" }
        },
        SearchTerm = "er",
        SortBy = "createdat",
        SortDescending = true
    };

    public BenchmarkMeasurement Measure()
    {
        for (var i = 0; i < WarmupOperations; i++)
            Consume(BuildPipeline());

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var stopwatch = Stopwatch.StartNew();

        for (var i = 0; i < MeasuredOperations; i++)
            Consume(BuildPipeline());

        stopwatch.Stop();
        var allocatedAfter = GC.GetAllocatedBytesForCurrentThread();

        return new BenchmarkMeasurement(
            MeasuredOperations,
            stopwatch.Elapsed.TotalMicroseconds / MeasuredOperations,
            (double)(allocatedAfter - allocatedBefore) / MeasuredOperations);
    }

    private IQueryable<BenchmarkRow> BuildPipeline()
        => QueryableHelper.ApplyFullPipeline(_context.Rows, _options);

    private static void Consume(IQueryable<BenchmarkRow> query)
        => _sink += (int)query.Expression.NodeType;
}

public readonly record struct BenchmarkMeasurement(int Operations, double MicrosecondsPerOperation, double BytesPerOperation);
