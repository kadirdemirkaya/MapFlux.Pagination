namespace MapFlux.Pagination.Models;

public record PaginationOptions
{
    private int _pageNumber = 1;
    private int _pageSize = 10;

    public int PageNumber
    {
        get => _pageNumber;
        init => _pageNumber = value < 1
            ? throw new ArgumentOutOfRangeException(nameof(PageNumber), value, "PageNumber must be greater than or equal to 1.")
            : value;
    }

    public int PageSize
    {
        get => _pageSize;
        init => _pageSize = value < 1
            ? throw new ArgumentOutOfRangeException(nameof(PageSize), value, "PageSize must be greater than or equal to 1.")
            : value;
    }

    public string? SortBy { get; init; }
    public bool SortDescending { get; init; }

    public IReadOnlyList<SortCriteria>? SortCriterias { get; init; }

    public IReadOnlyList<FilterCriteria>? Filters { get; init; }

    public string? SearchTerm { get; init; }

    public IReadOnlyList<string>? SearchProperties { get; init; }

    public bool StrictMode { get; init; }

    public int Skip
    {
        get
        {
            var skip = (long)(PageNumber - 1) * PageSize;
            return skip > int.MaxValue ? int.MaxValue : (int)skip;
        }
    }

    public int Take => PageSize;

    public PaginationOptions ClampPageSize(int maxPageSize)
    {
        if (PageSize <= maxPageSize)
            return this;

        return this with { PageSize = maxPageSize };
    }
}
