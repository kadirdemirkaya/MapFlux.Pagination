namespace MapFlux.Pagination.Models;

public enum FilterOperator
{
    Equals,
    NotEquals,
    Contains,
    StartsWith,
    EndsWith,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual
}

public record FilterCriteria
{
    public string PropertyName { get; init; } = string.Empty;
    public FilterOperator Operator { get; init; } = FilterOperator.Equals;
    public object? Value { get; init; }
}
