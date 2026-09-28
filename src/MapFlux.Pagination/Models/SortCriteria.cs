namespace MapFlux.Pagination.Models;

/// <summary>One property in a multi-property sort, applied via <see cref="PaginationOptions.SortCriterias"/>.</summary>
public record SortCriteria
{
    /// <summary>The property name to sort by, case-insensitive.</summary>
    public string PropertyName { get; init; } = string.Empty;

    /// <summary>Whether to sort descending instead of ascending.</summary>
    public bool Descending { get; init; }
}
