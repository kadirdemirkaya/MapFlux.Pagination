namespace MapFlux.Pagination.Core;

internal sealed class ProjectedCursorRow<TItem>
{
    public object? CursorValue { get; set; }

    public object? SortValue { get; set; }

    public TItem Item { get; set; } = default!;
}
