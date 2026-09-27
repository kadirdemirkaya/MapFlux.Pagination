namespace MapFlux.Pagination.Models;

public interface IPagedResult<T>
{
    IReadOnlyList<T> Items { get; }
    int TotalCount { get; }
    int PageNumber { get; }
    int PageSize { get; }
    int TotalPages { get; }
    bool HasPreviousPage { get; }
    bool HasNextPage { get; }
}

public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int PageNumber,
    int PageSize) : IPagedResult<T>
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;

    public static PagedResult<T> Empty(int pageSize = 10)
        => new(Array.Empty<T>(), 0, 1, pageSize);

    public int FirstItemIndex => TotalCount == 0 ? 0 : (PageNumber - 1) * PageSize + 1;

    public int LastItemIndex => TotalCount == 0 ? 0 : Math.Min(PageNumber * PageSize, TotalCount);
}
