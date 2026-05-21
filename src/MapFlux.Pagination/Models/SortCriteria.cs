namespace MapFlux.Pagination.Models;

public record SortCriteria
{
    public string PropertyName { get; init; } = string.Empty;
    public bool Descending { get; init; }
}
