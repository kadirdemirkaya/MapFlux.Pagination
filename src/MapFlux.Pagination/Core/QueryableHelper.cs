using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using MapFlux.Pagination.Exceptions;
using MapFlux.Pagination.Models;

namespace MapFlux.Pagination.Core;

public static class QueryableHelper
{
    public static IQueryable<T> ApplySorting<T>(IQueryable<T> source, string? sortBy, bool descending)
        => ApplySorting(source, sortBy, descending, strict: false);

    public static IQueryable<T> ApplySorting<T>(IQueryable<T> source, string? sortBy, bool descending, bool strict)
    {
        if (string.IsNullOrWhiteSpace(sortBy))
            return source;

        var property = FindSortableProperty<T>(sortBy);

        if (property == null)
        {
            if (strict)
                throw new PaginationStrictModeException(
                    $"Unknown sort property '{sortBy}' on type '{typeof(T)}'.", sortBy, null, null);

            return source;
        }

        return OrderByProperty(source, property, descending);
    }

    public static IQueryable<T> ApplyMultiSorting<T>(IQueryable<T> source, IReadOnlyList<SortCriteria>? sortCriterias)
        => ApplyMultiSorting(source, sortCriterias, strict: false);

    public static IQueryable<T> ApplyMultiSorting<T>(IQueryable<T> source, IReadOnlyList<SortCriteria>? sortCriterias, bool strict)
    {
        if (sortCriterias == null || sortCriterias.Count == 0)
            return source;

        var type = typeof(T);
        IQueryable<T> result = source;
        var hasOrder = false;

        for (int i = 0; i < sortCriterias.Count; i++)
        {
            var criteria = sortCriterias[i];
            var property = type.GetProperty(criteria.PropertyName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);

            if (property == null)
            {
                if (strict)
                    throw new PaginationStrictModeException(
                        $"Unknown sort property '{criteria.PropertyName}' on type '{type}'.", criteria.PropertyName, null, null);

                continue;
            }

            var parameter = Expression.Parameter(type, "p");
            var propertyAccess = Expression.MakeMemberAccess(parameter, property);
            var orderByExp = Expression.Lambda(propertyAccess, parameter);

            string methodName;
            if (!hasOrder)
                methodName = criteria.Descending ? "OrderByDescending" : "OrderBy";
            else
                methodName = criteria.Descending ? "ThenByDescending" : "ThenBy";

            var resultExp = Expression.Call(
                typeof(Queryable),
                methodName,
                new[] { type, property.PropertyType },
                result.Expression,
                Expression.Quote(orderByExp));

            result = result.Provider.CreateQuery<T>(resultExp);
            hasOrder = true;
        }

        return result;
    }

    public static IQueryable<T> ApplyFiltering<T>(IQueryable<T> source, IReadOnlyList<FilterCriteria>? filters)
        => ApplyFiltering(source, filters, strict: false);

    public static IQueryable<T> ApplyFiltering<T>(IQueryable<T> source, IReadOnlyList<FilterCriteria>? filters, bool strict)
    {
        if (filters == null || filters.Count == 0)
            return source;

        var type = typeof(T);
        var result = source;

        foreach (var filter in filters)
        {
            var property = type.GetProperty(filter.PropertyName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
            if (property == null)
            {
                if (strict)
                    throw new PaginationStrictModeException(
                        $"Unknown filter property '{filter.PropertyName}' on type '{type}'.", filter.PropertyName, filter.Value, null);

                continue;
            }

            var parameter = Expression.Parameter(type, "p");
            var propertyAccess = Expression.MakeMemberAccess(parameter, property);
            var filterExpression = BuildFilterExpression(propertyAccess, filter, property.PropertyType, strict);

            if (filterExpression == null)
                continue;

            var lambda = Expression.Lambda<Func<T, bool>>(filterExpression, parameter);
            result = result.Where(lambda);
        }

        return result;
    }

    public static IQueryable<T> ApplySearch<T>(IQueryable<T> source, string? searchTerm, IReadOnlyList<string>? searchProperties)
        => ApplySearch(source, searchTerm, searchProperties, strict: false);

    public static IQueryable<T> ApplySearch<T>(IQueryable<T> source, string? searchTerm, IReadOnlyList<string>? searchProperties, bool strict)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return source;

        var type = typeof(T);
        var parameter = Expression.Parameter(type, "p");

        List<PropertyInfo> properties;
        if (searchProperties != null && searchProperties.Count > 0)
        {
            properties = new List<PropertyInfo>();
            foreach (var name in searchProperties)
            {
                var candidate = type.GetProperty(name, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                if (candidate == null || candidate.PropertyType != typeof(string))
                {
                    if (strict)
                        throw new PaginationStrictModeException(
                            $"Unknown search property '{name}' on type '{type}'; expected an existing public string property.",
                            name, null, candidate?.PropertyType);

                    continue;
                }

                properties.Add(candidate);
            }
        }
        else
        {
            properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.PropertyType == typeof(string))
                .ToList();
        }

        if (properties.Count == 0)
            return source;

        var containsMethod = typeof(string).GetMethod("Contains", new[] { typeof(string) })!;
        var toLowerMethod = typeof(string).GetMethod("ToLower", Type.EmptyTypes)!;
        var searchValue = Expression.Constant(searchTerm.ToLowerInvariant());

        Expression? combinedExpression = null;

        foreach (var prop in properties)
        {
            if (prop == null) continue;

            var propertyAccess = Expression.MakeMemberAccess(parameter, prop);
            var nullCheck = Expression.NotEqual(propertyAccess, Expression.Constant(null, typeof(string)));
            var toLower = Expression.Call(propertyAccess, toLowerMethod);
            var containsCall = Expression.Call(toLower, containsMethod, searchValue);
            var safeContains = Expression.AndAlso(nullCheck, containsCall);

            combinedExpression = combinedExpression == null
                ? safeContains
                : Expression.OrElse(combinedExpression, safeContains);
        }

        if (combinedExpression == null)
            return source;

        var lambda = Expression.Lambda<Func<T, bool>>(combinedExpression, parameter);
        return source.Where(lambda);
    }

    public static IQueryable<T> ApplyFullPipeline<T>(IQueryable<T> source, PaginationOptions opts)
    {
        var query = ApplyFiltering(source, opts.Filters, opts.StrictMode);

        query = ApplySearch(query, opts.SearchTerm, opts.SearchProperties, opts.StrictMode);

        bool ordered;

        if (opts.SortCriterias != null && opts.SortCriterias.Count > 0)
        {
            query = ApplyMultiSorting(query, opts.SortCriterias, opts.StrictMode);
            ordered = opts.SortCriterias.Any(criteria => FindSortableProperty<T>(criteria.PropertyName) != null);
        }
        else
        {
            query = ApplySorting(query, opts.SortBy, opts.SortDescending, opts.StrictMode);
            ordered = FindSortableProperty<T>(opts.SortBy) != null;
        }

        if (!ordered)
            query = ApplyDefaultOrdering(query, opts);

        return query;
    }

    public static IQueryable<T> ApplyDefaultOrdering<T>(IQueryable<T> source, PaginationOptions opts)
    {
        if (!string.IsNullOrWhiteSpace(opts.DefaultSortProperty))
        {
            var configured = FindSortableProperty<T>(opts.DefaultSortProperty);

            if (configured != null)
                return OrderByProperty(source, configured, descending: false);

            if (opts.StrictMode)
                throw new PaginationStrictModeException(
                    $"Unknown default sort property '{opts.DefaultSortProperty}' on type '{typeof(T)}'.",
                    opts.DefaultSortProperty!, null, null);
        }

        if (!opts.EnsureDeterministicOrder)
            return source;

        var key = FindKeyProperty<T>();

        if (key != null)
            return OrderByProperty(source, key, descending: false);

        if (opts.StrictMode)
            throw new PaginationStrictModeException(
                $"No key property could be resolved on type '{typeof(T)}' to order by. Set DefaultSortProperty.",
                string.Empty, null, typeof(T));

        return source;
    }

    private static PropertyInfo? FindSortableProperty<T>(string? propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
            return null;

        return typeof(T).GetProperty(propertyName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
    }

    private static PropertyInfo? FindKeyProperty<T>()
    {
        var type = typeof(T);

        var annotated = type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(property => property.CanRead && property.IsDefined(typeof(KeyAttribute), inherit: true));

        if (annotated != null)
            return annotated;

        return FindSortableProperty<T>("Id") ?? FindSortableProperty<T>($"{type.Name}Id");
    }

    private static IQueryable<T> OrderByProperty<T>(IQueryable<T> source, PropertyInfo property, bool descending)
    {
        var parameter = Expression.Parameter(typeof(T), "p");
        var propertyAccess = Expression.MakeMemberAccess(parameter, property);
        var orderByExp = Expression.Lambda(propertyAccess, parameter);

        var resultExp = Expression.Call(
            typeof(Queryable),
            descending ? "OrderByDescending" : "OrderBy",
            new[] { typeof(T), property.PropertyType },
            source.Expression,
            Expression.Quote(orderByExp));

        return source.Provider.CreateQuery<T>(resultExp);
    }

    public static IQueryable<T> ApplyCursorFilter<T>(IQueryable<T> source, CursorPaginationOptions opts)
    {
        var type = typeof(T);
        var cursorProperty = type.GetProperty(opts.CursorProperty, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);

        if (cursorProperty == null)
            return source;

        var query = source;

        if (!string.IsNullOrWhiteSpace(opts.SortBy))
            query = ApplySorting(query, opts.SortBy, opts.SortDescending);
        else
            query = ApplySorting(query, opts.CursorProperty, false);

        if (!string.IsNullOrWhiteSpace(opts.After))
        {
            var cursorValue = DecodeCursor(opts.After, cursorProperty.PropertyType);
            if (cursorValue != null)
            {
                var parameter = Expression.Parameter(type, "p");
                var propertyAccess = Expression.MakeMemberAccess(parameter, cursorProperty);
                var constant = Expression.Constant(cursorValue, cursorProperty.PropertyType);
                var comparison = Expression.GreaterThan(propertyAccess, constant);
                var lambda = Expression.Lambda<Func<T, bool>>(comparison, parameter);
                query = query.Where(lambda);
            }
        }
        else if (!string.IsNullOrWhiteSpace(opts.Before))
        {
            var cursorValue = DecodeCursor(opts.Before, cursorProperty.PropertyType);
            if (cursorValue != null)
            {
                var parameter = Expression.Parameter(type, "p");
                var propertyAccess = Expression.MakeMemberAccess(parameter, cursorProperty);
                var constant = Expression.Constant(cursorValue, cursorProperty.PropertyType);
                var comparison = Expression.LessThan(propertyAccess, constant);
                var lambda = Expression.Lambda<Func<T, bool>>(comparison, parameter);
                query = query.Where(lambda);
            }
        }

        return query;
    }

    public static string EncodeCursor(object value)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(value.ToString()!);
        return Convert.ToBase64String(bytes);
    }

    public static object? DecodeCursor(string cursor, Type targetType)
    {
        try
        {
            var bytes = Convert.FromBase64String(cursor);
            var stringValue = System.Text.Encoding.UTF8.GetString(bytes);

            if (targetType == typeof(int))
                return int.Parse(stringValue);
            if (targetType == typeof(long))
                return long.Parse(stringValue);
            if (targetType == typeof(Guid))
                return Guid.Parse(stringValue);
            if (targetType == typeof(string))
                return stringValue;
            if (targetType == typeof(DateTime))
                return DateTime.Parse(stringValue);
            if (targetType == typeof(DateTimeOffset))
                return DateTimeOffset.Parse(stringValue);

            return Convert.ChangeType(stringValue, targetType);
        }
        catch
        {
            return null;
        }
    }

    private static Expression? BuildFilterExpression(Expression propertyAccess, FilterCriteria filter, Type propertyType, bool strict)
    {
        var op = filter.Operator;
        var value = filter.Value;
        var propertyName = filter.PropertyName;

        if (value is JsonElement nullCandidate && IsJsonNull(nullCandidate))
        {
            if (!AcceptsNull(propertyType))
            {
                if (strict)
                    throw new PaginationStrictModeException(
                        $"Filter value 'null' cannot be applied to non-nullable property '{propertyName}' of type '{propertyType}'.",
                        propertyName, null, propertyType);

                return null;
            }

            value = null;
        }

        if (value == null && op != FilterOperator.Equals && op != FilterOperator.NotEquals)
        {
            if (strict)
                throw new PaginationStrictModeException(
                    $"Filter operator '{op}' cannot be used with a null value on property '{propertyName}'.",
                    propertyName, null, propertyType);

            return null;
        }

        if (value == null && !AcceptsNull(propertyType))
        {
            if (strict)
                throw new PaginationStrictModeException(
                    $"Filter value 'null' cannot be applied to non-nullable property '{propertyName}' of type '{propertyType}'.",
                    propertyName, null, propertyType);

            return Expression.Constant(op == FilterOperator.NotEquals);
        }

        Expression constant;
        if (value == null)
        {
            constant = Expression.Constant(null, propertyType);
        }
        else
        {
            var convertedValue = ConvertValue(value, propertyType);
            if (convertedValue == null)
            {
                if (strict)
                    throw new PaginationStrictModeException(
                        $"Filter value '{value}' on property '{propertyName}' could not be converted to type '{propertyType}'.",
                        propertyName, value, propertyType);

                return null;
            }
            constant = Expression.Constant(convertedValue, propertyType);
        }

        switch (op)
        {
            case FilterOperator.Equals:
                return Expression.Equal(propertyAccess, constant);

            case FilterOperator.NotEquals:
                return Expression.NotEqual(propertyAccess, constant);

            case FilterOperator.GreaterThan:
                return Expression.GreaterThan(propertyAccess, constant);

            case FilterOperator.GreaterThanOrEqual:
                return Expression.GreaterThanOrEqual(propertyAccess, constant);

            case FilterOperator.LessThan:
                return Expression.LessThan(propertyAccess, constant);

            case FilterOperator.LessThanOrEqual:
                return Expression.LessThanOrEqual(propertyAccess, constant);

            case FilterOperator.Contains:
            case FilterOperator.StartsWith:
            case FilterOperator.EndsWith:
            {
                if (propertyType != typeof(string))
                {
                    if (strict)
                        throw new PaginationStrictModeException(
                            $"Filter operator '{op}' is not supported on property '{propertyName}' of type '{propertyType}'; it requires a string property.",
                            propertyName, value, propertyType);

                    return null;
                }

                var methodName = op switch
                {
                    FilterOperator.Contains => "Contains",
                    FilterOperator.StartsWith => "StartsWith",
                    FilterOperator.EndsWith => "EndsWith",
                    _ => throw new InvalidOperationException()
                };

                var method = typeof(string).GetMethod(methodName, new[] { typeof(string) })!;
                var nullCheck = Expression.NotEqual(propertyAccess, Expression.Constant(null, typeof(string)));
                var methodCall = Expression.Call(propertyAccess, method, constant);
                return Expression.AndAlso(nullCheck, methodCall);
            }

            default:
                if (strict)
                    throw new PaginationStrictModeException(
                        $"Unsupported filter operator '{op}' on property '{propertyName}'.", propertyName, value, propertyType);

                return null;
        }
    }

    private static object? ConvertValue(object value, Type targetType)
    {
        try
        {
            var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

            if (value is JsonElement jsonElement)
                return ConvertJsonElement(jsonElement, underlyingType);

            if (value.GetType() == underlyingType)
                return value;

            if (underlyingType.IsEnum)
                return ConvertToEnum(value, underlyingType);

            if (value is string text)
                return ConvertFromString(text, underlyingType);

            return Convert.ChangeType(value, underlyingType);
        }
        catch
        {
            return null;
        }
    }

    private static object? ConvertFromString(string text, Type targetType)
    {
        if (targetType == typeof(Guid))
            return Guid.TryParse(text, out var guid) ? guid : (object?)null;

        if (targetType == typeof(DateTime))
            return ParseDateTime(text, CultureInfo.InvariantCulture) ?? ParseDateTime(text, CultureInfo.CurrentCulture);

        if (targetType == typeof(DateTimeOffset))
            return ParseDateTimeOffset(text, CultureInfo.InvariantCulture) ?? ParseDateTimeOffset(text, CultureInfo.CurrentCulture);

        if (targetType == typeof(DateOnly))
            return ParseDateOnly(text, CultureInfo.InvariantCulture) ?? ParseDateOnly(text, CultureInfo.CurrentCulture);

        if (targetType == typeof(TimeOnly))
            return ParseTimeOnly(text, CultureInfo.InvariantCulture) ?? ParseTimeOnly(text, CultureInfo.CurrentCulture);

        if (IsFloatingPoint(targetType))
            return ParseFloatingPoint(text, targetType, NumberStyles.Float, CultureInfo.InvariantCulture)
                ?? ParseFloatingPoint(text, targetType, NumberStyles.Number, CultureInfo.CurrentCulture);

        return ChangeType(text, targetType, CultureInfo.InvariantCulture) ?? ChangeType(text, targetType, CultureInfo.CurrentCulture);
    }

    private static object? ParseDateTime(string text, CultureInfo culture)
        => DateTime.TryParse(text, culture, DateTimeStyles.RoundtripKind, out var value) ? value : (object?)null;

    private static object? ParseDateTimeOffset(string text, CultureInfo culture)
        => DateTimeOffset.TryParse(text, culture, DateTimeStyles.RoundtripKind, out var value) ? value : (object?)null;

    private static object? ParseDateOnly(string text, CultureInfo culture)
        => DateOnly.TryParse(text, culture, DateTimeStyles.None, out var value) ? value : (object?)null;

    private static object? ParseTimeOnly(string text, CultureInfo culture)
        => TimeOnly.TryParse(text, culture, DateTimeStyles.None, out var value) ? value : (object?)null;

    private static bool IsFloatingPoint(Type type)
        => type == typeof(decimal) || type == typeof(double) || type == typeof(float);

    private static object? ParseFloatingPoint(string text, Type targetType, NumberStyles styles, CultureInfo culture)
    {
        if (targetType == typeof(decimal))
            return decimal.TryParse(text, styles, culture, out var decimalValue) ? decimalValue : (object?)null;

        if (targetType == typeof(double))
            return double.TryParse(text, styles, culture, out var doubleValue) ? doubleValue : (object?)null;

        return float.TryParse(text, styles, culture, out var floatValue) ? floatValue : (object?)null;
    }

    private static object? ChangeType(string text, Type targetType, CultureInfo culture)
    {
        try
        {
            return Convert.ChangeType(text, targetType, culture);
        }
        catch
        {
            return null;
        }
    }

    private static object? ConvertToEnum(object value, Type enumType)
    {
        if (value is string text)
            return Enum.TryParse(enumType, text, ignoreCase: true, out var parsed) ? parsed : null;

        var numeric = Convert.ToInt64(value);
        return Enum.IsDefined(enumType, Enum.ToObject(enumType, numeric)) ? Enum.ToObject(enumType, numeric) : null;
    }

    private static object? ConvertJsonElement(JsonElement element, Type underlyingType)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                var text = element.GetString();
                if (text == null)
                    return null;
                return underlyingType == typeof(string) ? text : ConvertValue(text, underlyingType);

            case JsonValueKind.Number:
                return ConvertJsonNumber(element, underlyingType);

            case JsonValueKind.True:
            case JsonValueKind.False:
                var flag = element.GetBoolean();
                return underlyingType == typeof(bool) ? flag : ConvertValue(flag, underlyingType);

            default:
                return null;
        }
    }

    private static object? ConvertJsonNumber(JsonElement element, Type underlyingType)
    {
        if (underlyingType == typeof(int))
            return element.GetInt32();
        if (underlyingType == typeof(long))
            return element.GetInt64();
        if (underlyingType == typeof(short))
            return element.GetInt16();
        if (underlyingType == typeof(byte))
            return element.GetByte();
        if (underlyingType == typeof(sbyte))
            return element.GetSByte();
        if (underlyingType == typeof(uint))
            return element.GetUInt32();
        if (underlyingType == typeof(ulong))
            return element.GetUInt64();
        if (underlyingType == typeof(ushort))
            return element.GetUInt16();
        if (underlyingType == typeof(decimal))
            return element.GetDecimal();
        if (underlyingType == typeof(double))
            return element.GetDouble();
        if (underlyingType == typeof(float))
            return element.GetSingle();
        if (underlyingType == typeof(string))
            return element.GetRawText();

        return ConvertValue(element.GetDecimal(), underlyingType);
    }

    private static bool IsJsonNull(JsonElement element)
        => element.ValueKind == JsonValueKind.Null || element.ValueKind == JsonValueKind.Undefined;

    private static bool AcceptsNull(Type type)
        => !type.IsValueType || Nullable.GetUnderlyingType(type) != null;
}
