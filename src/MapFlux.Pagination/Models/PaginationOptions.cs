namespace MapFlux.Pagination.Models;

/// <summary>
/// Requests one offset page from <c>ToPagedAsync</c>/<c>ToPaged</c>/<c>MapPagedAsync</c>, plus the
/// filter, search and sort criteria applied to it.
/// </summary>
public record PaginationOptions
{
    private int _pageNumber = 1;
    private int _pageSize = 10;

    /// <summary>The 1-based page to read.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The assigned value is less than 1.</exception>
    public int PageNumber
    {
        get => _pageNumber;
        init => _pageNumber = value < 1
            ? throw new ArgumentOutOfRangeException(nameof(PageNumber), value, "PageNumber must be greater than or equal to 1.")
            : value;
    }

    /// <summary>The number of items per page.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The assigned value is less than 1.</exception>
    public int PageSize
    {
        get => _pageSize;
        init => _pageSize = value < 1
            ? throw new ArgumentOutOfRangeException(nameof(PageSize), value, "PageSize must be greater than or equal to 1.")
            : value;
    }

    /// <summary>The single property name to sort by, case-insensitive. Ignored when <see cref="SortCriterias"/> is set.</summary>
    public string? SortBy { get; init; }

    /// <summary>Whether <see cref="SortBy"/> orders descending instead of ascending.</summary>
    public bool SortDescending { get; init; }

    /// <summary>The ordered multi-property sort, applied instead of <see cref="SortBy"/>/<see cref="SortDescending"/> when set.</summary>
    public IReadOnlyList<SortCriteria>? SortCriterias { get; init; }

    /// <summary>The filters applied before search and sort.</summary>
    public IReadOnlyList<FilterCriteria>? Filters { get; init; }

    /// <summary>The term matched, case-insensitively, against <see cref="SearchProperties"/>.</summary>
    public string? SearchTerm { get; init; }

    /// <summary>The <see cref="string"/> properties <see cref="SearchTerm"/> is matched against, or every public <see cref="string"/> property when unset.</summary>
    public IReadOnlyList<string>? SearchProperties { get; init; }

    /// <summary>When <see langword="true"/>, an unknown property, an inconvertible filter value or an unsupported operator throws instead of being silently ignored.</summary>
    public bool StrictMode { get; init; }

    /// <summary>The property to order by when the request produced no ordering.</summary>
    public string? DefaultSortProperty { get; init; }

    /// <summary>When <see langword="true"/> and no ordering was produced, orders by the resolved key property so the page is deterministic.</summary>
    public bool EnsureDeterministicOrder { get; init; }

    /// <summary>The number of items to skip to reach <see cref="PageNumber"/>, clamped to <see cref="int.MaxValue"/> to avoid overflow.</summary>
    public int Skip
    {
        get
        {
            var skip = (long)(PageNumber - 1) * PageSize;
            return skip > int.MaxValue ? int.MaxValue : (int)skip;
        }
    }

    /// <summary>The number of items to take, equal to <see cref="PageSize"/>.</summary>
    public int Take => PageSize;

    /// <summary>
    /// Returns this instance, or a copy with <see cref="PageSize"/> reduced to <paramref name="maxPageSize"/>
    /// when it exceeds it.
    /// </summary>
    /// <param name="maxPageSize">The upper bound to clamp <see cref="PageSize"/> to.</param>
    /// <returns>This instance, or a clamped copy.</returns>
    public PaginationOptions ClampPageSize(int maxPageSize)
    {
        if (PageSize <= maxPageSize)
            return this;

        return this with { PageSize = maxPageSize };
    }
}
