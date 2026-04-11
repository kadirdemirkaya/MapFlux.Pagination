namespace MapFlux.Pagination.Models;

public record PaginationOptions
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;

    public string? SortBy { get; init; }
    public bool SortDescending { get; init; }

    public int Skip => (PageNumber - 1) * PageSize;
    public int Take => PageSize;
}
