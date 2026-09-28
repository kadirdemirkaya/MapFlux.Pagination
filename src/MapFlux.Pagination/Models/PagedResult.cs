namespace MapFlux.Pagination.Models;

/// <summary>An offset-paged result: one page of items plus the total count and page metadata.</summary>
/// <typeparam name="T">The item type of the page.</typeparam>
public interface IPagedResult<T>
{
    /// <summary>The items on this page.</summary>
    IReadOnlyList<T> Items { get; }

    /// <summary>The total number of items across every page.</summary>
    int TotalCount { get; }

    /// <summary>The 1-based number of this page.</summary>
    int PageNumber { get; }

    /// <summary>The number of items requested per page.</summary>
    int PageSize { get; }

    /// <summary>The total number of pages.</summary>
    int TotalPages { get; }

    /// <summary>Whether a page before this one exists.</summary>
    bool HasPreviousPage { get; }

    /// <summary>Whether a page after this one exists.</summary>
    bool HasNextPage { get; }
}

/// <summary>An offset-paged result: one page of items plus the total count and page metadata.</summary>
/// <typeparam name="T">The item type of the page.</typeparam>
/// <param name="Items">The items on this page.</param>
/// <param name="TotalCount">The total number of items across every page.</param>
/// <param name="PageNumber">The 1-based number of this page.</param>
/// <param name="PageSize">The number of items requested per page.</param>
public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int PageNumber,
    int PageSize) : IPagedResult<T>
{
    /// <inheritdoc />
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    /// <inheritdoc />
    public bool HasPreviousPage => PageNumber > 1;

    /// <inheritdoc />
    public bool HasNextPage => PageNumber < TotalPages;

    /// <summary>Creates an empty page: no items, page 1 of 0.</summary>
    /// <param name="pageSize">The page size to report.</param>
    /// <returns>An empty page.</returns>
    public static PagedResult<T> Empty(int pageSize = 10)
        => new(Array.Empty<T>(), 0, 1, pageSize);

    /// <summary>The 1-based index of the first item on this page, or 0 when the result is empty.</summary>
    public int FirstItemIndex => TotalCount == 0 ? 0 : (PageNumber - 1) * PageSize + 1;

    /// <summary>The 1-based index of the last item on this page, or 0 when the result is empty.</summary>
    public int LastItemIndex => TotalCount == 0 ? 0 : Math.Min(PageNumber * PageSize, TotalCount);
}
