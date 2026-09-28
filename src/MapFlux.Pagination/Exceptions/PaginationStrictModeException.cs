namespace MapFlux.Pagination.Exceptions;

/// <summary>
/// Thrown when strict mode is enabled and a filter, search or sort request references an unknown
/// property, a value that cannot be converted to the target member's type, or an operator that does
/// not apply to that type.
/// </summary>
public sealed class PaginationStrictModeException : InvalidOperationException
{
    /// <summary>The property name the request referenced.</summary>
    public string PropertyName { get; }

    /// <summary>The value that could not be applied, or <see langword="null"/>.</summary>
    public object? Value { get; }

    /// <summary>The member type the value or property was checked against, or <see langword="null"/>.</summary>
    public Type? TargetType { get; }

    /// <summary>
    /// Creates the exception for a strict-mode violation.
    /// </summary>
    /// <param name="message">A message describing what failed.</param>
    /// <param name="propertyName">The property name the request referenced.</param>
    /// <param name="value">The value that could not be applied, or <see langword="null"/>.</param>
    /// <param name="targetType">The member type the value or property was checked against, or <see langword="null"/>.</param>
    public PaginationStrictModeException(string message, string propertyName, object? value, Type? targetType)
        : base(message)
    {
        PropertyName = propertyName;
        Value = value;
        TargetType = targetType;
    }
}
