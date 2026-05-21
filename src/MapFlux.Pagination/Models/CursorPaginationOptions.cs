namespace MapFlux.Pagination.Models;

public record CursorPaginationOptions
{
    public string? After { get; init; }
    public string? Before { get; init; }
    public int PageSize { get; init; } = 10;
    public string CursorProperty { get; init; } = "Id";
    public string? SortBy { get; init; }
    public bool SortDescending { get; init; }
}
