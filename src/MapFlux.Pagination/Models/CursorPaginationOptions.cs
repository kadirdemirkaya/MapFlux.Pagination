namespace MapFlux.Pagination.Models;

/// <summary>
/// Requests one keyset (cursor) page from <c>ToCursorPagedAsync</c>/<c>MapCursorPagedAsync</c>.
/// </summary>
public record CursorPaginationOptions
{
    /// <summary>The cursor to read forward from, or <see langword="null"/> for the first page.</summary>
    public string? After { get; init; }

    /// <summary>The cursor to read backward from. Ignored when <see cref="After"/> is set.</summary>
    public string? Before { get; init; }

    /// <summary>The number of items per page.</summary>
    public int PageSize { get; init; } = 10;

    /// <summary>The property the cursor value is read from and compared against.</summary>
    public string CursorProperty { get; init; } = "Id";

    /// <summary>An additional property to order by before <see cref="CursorProperty"/> as a tie-breaker.</summary>
    public string? SortBy { get; init; }

    /// <summary>Whether <see cref="SortBy"/> orders descending instead of ascending.</summary>
    public bool SortDescending { get; init; }

    /// <summary>When <see langword="true"/>, an unknown property or an undecodable cursor throws instead of being silently ignored.</summary>
    public bool StrictMode { get; init; }

    /// <summary>Whether the total row count is read alongside the page. Set <see langword="false"/> to skip the extra <c>COUNT(*)</c>.</summary>
    public bool IncludeTotalCount { get; init; } = true;

    /// <summary>
    /// Returns this instance, or a copy with <see cref="PageSize"/> reduced to <paramref name="maxPageSize"/>
    /// when it exceeds it.
    /// </summary>
    /// <param name="maxPageSize">The upper bound to clamp <see cref="PageSize"/> to.</param>
    /// <returns>This instance, or a clamped copy.</returns>
    public CursorPaginationOptions ClampPageSize(int maxPageSize)
    {
        if (PageSize <= maxPageSize)
            return this;

        return this with { PageSize = maxPageSize };
    }
}
