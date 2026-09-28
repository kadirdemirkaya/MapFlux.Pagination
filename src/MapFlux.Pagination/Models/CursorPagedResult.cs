namespace MapFlux.Pagination.Models;

/// <summary>A keyset (cursor) paged result: one page of items plus the cursors to read the next/previous page.</summary>
/// <typeparam name="T">The item type of the page.</typeparam>
public interface ICursorPagedResult<T>
{
    /// <summary>The items on this page.</summary>
    IReadOnlyList<T> Items { get; }

    /// <summary>The cursor pointing at the first item on this page, or <see langword="null"/> when the page is empty.</summary>
    string? StartCursor { get; }

    /// <summary>The cursor pointing at the last item on this page, or <see langword="null"/> when the page is empty.</summary>
    string? EndCursor { get; }

    /// <summary>Whether a page after this one exists.</summary>
    bool HasNextPage { get; }

    /// <summary>Whether a page before this one exists.</summary>
    bool HasPreviousPage { get; }

    /// <summary>The total number of items across every page, or <c>-1</c> when <see cref="CursorPaginationOptions.IncludeTotalCount"/> was <see langword="false"/>.</summary>
    int TotalCount { get; }
}

/// <summary>A keyset (cursor) paged result: one page of items plus the cursors to read the next/previous page.</summary>
/// <typeparam name="T">The item type of the page.</typeparam>
/// <param name="Items">The items on this page.</param>
/// <param name="StartCursor">The cursor pointing at the first item on this page, or <see langword="null"/> when the page is empty.</param>
/// <param name="EndCursor">The cursor pointing at the last item on this page, or <see langword="null"/> when the page is empty.</param>
/// <param name="HasNextPage">Whether a page after this one exists.</param>
/// <param name="HasPreviousPage">Whether a page before this one exists.</param>
/// <param name="TotalCount">The total number of items across every page, or <c>-1</c> when the total was not requested.</param>
public record CursorPagedResult<T>(
    IReadOnlyList<T> Items,
    string? StartCursor,
    string? EndCursor,
    bool HasNextPage,
    bool HasPreviousPage,
    int TotalCount) : ICursorPagedResult<T>
{
    /// <summary>Creates an empty page: no items, no cursors, no next or previous page.</summary>
    /// <returns>An empty page.</returns>
    public static CursorPagedResult<T> Empty() =>
        new(Array.Empty<T>(), null, null, false, false, 0);
}
