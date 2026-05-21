namespace MapFlux.Pagination.Models;

public class PaginationGlobalOptions
{
    public int DefaultPageSize { get; set; } = 10;
    public int MaxPageSize { get; set; } = 100;
    public int DefaultPageNumber { get; set; } = 1;
}
