namespace MapFlux.Pagination.Models;

public interface ICursorPagedResult<T>
{
    IReadOnlyList<T> Items { get; }
    string? StartCursor { get; }
    string? EndCursor { get; }
    bool HasNextPage { get; }
    bool HasPreviousPage { get; }
    int TotalCount { get; }
}

public record CursorPagedResult<T>(
    IReadOnlyList<T> Items,
    string? StartCursor,
    string? EndCursor,
    bool HasNextPage,
    bool HasPreviousPage,
    int TotalCount) : ICursorPagedResult<T>
{
    public static CursorPagedResult<T> Empty() =>
        new(Array.Empty<T>(), null, null, false, false, 0);
}
