namespace MapFlux.Pagination.Models;

/// <summary>The comparison a <see cref="FilterCriteria"/> applies between a property and its value.</summary>
public enum FilterOperator
{
    /// <summary>The property equals the value. The only operator that accepts a <see langword="null"/> value.</summary>
    Equals,

    /// <summary>The property does not equal the value. Accepts a <see langword="null"/> value.</summary>
    NotEquals,

    /// <summary>The <see cref="string"/> property contains the value as a substring.</summary>
    Contains,

    /// <summary>The <see cref="string"/> property starts with the value.</summary>
    StartsWith,

    /// <summary>The <see cref="string"/> property ends with the value.</summary>
    EndsWith,

    /// <summary>The property is greater than the value.</summary>
    GreaterThan,

    /// <summary>The property is greater than or equal to the value.</summary>
    GreaterThanOrEqual,

    /// <summary>The property is less than the value.</summary>
    LessThan,

    /// <summary>The property is less than or equal to the value.</summary>
    LessThanOrEqual
}

/// <summary>One property/operator/value clause applied as a <c>Where</c> filter during pagination.</summary>
public record FilterCriteria
{
    /// <summary>The property name to filter on, case-insensitive.</summary>
    public string PropertyName { get; init; } = string.Empty;

    /// <summary>The comparison applied between the property and <see cref="Value"/>.</summary>
    public FilterOperator Operator { get; init; } = FilterOperator.Equals;

    /// <summary>The value compared against the property. Only <see cref="FilterOperator.Equals"/> and <see cref="FilterOperator.NotEquals"/> accept <see langword="null"/>.</summary>
    public object? Value { get; init; }
}
