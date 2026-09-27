using System.Globalization;
using MapFlux.Pagination.Benchmarks;

var measurement = new PipelineBenchmark().Measure();

Console.WriteLine(string.Format(
    CultureInfo.InvariantCulture,
    "ApplyFullPipeline (filter + search + sort): {0:0.00} us/op, {1:0} B/op over {2} operations",
    measurement.MicrosecondsPerOperation,
    measurement.BytesPerOperation,
    measurement.Operations));
