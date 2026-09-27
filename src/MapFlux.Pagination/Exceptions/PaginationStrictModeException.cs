namespace MapFlux.Pagination.Exceptions;

/// <summary>
/// Thrown when strict mode is enabled and a filter, search or sort request references an unknown
/// property, a value that cannot be converted to the target member's type, or an operator that does
/// not apply to that type.
/// </summary>
public sealed class PaginationStrictModeException : InvalidOperationException
{
    public string PropertyName { get; }
    public object? Value { get; }
    public Type? TargetType { get; }

    public PaginationStrictModeException(string message, string propertyName, object? value, Type? targetType)
        : base(message)
    {
        PropertyName = propertyName;
        Value = value;
        TargetType = targetType;
    }
}
