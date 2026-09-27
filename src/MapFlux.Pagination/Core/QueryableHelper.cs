using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using MapFlux.Pagination.Models;

namespace MapFlux.Pagination.Core;

public static class QueryableHelper
{
    public static IQueryable<T> ApplySorting<T>(IQueryable<T> source, string? sortBy, bool descending)
    {
        if (string.IsNullOrWhiteSpace(sortBy))
            return source;

        var type = typeof(T);
        var property = type.GetProperty(sortBy, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);

        if (property == null)
            return source;

        var parameter = Expression.Parameter(type, "p");
        var propertyAccess = Expression.MakeMemberAccess(parameter, property);
        var orderByExp = Expression.Lambda(propertyAccess, parameter);

        var methodName = descending ? "OrderByDescending" : "OrderBy";
        var resultExp = Expression.Call(
            typeof(Queryable),
            methodName,
            new[] { type, property.PropertyType },
            source.Expression,
            Expression.Quote(orderByExp));

        return source.Provider.CreateQuery<T>(resultExp);
    }

    public static IQueryable<T> ApplyMultiSorting<T>(IQueryable<T> source, IReadOnlyList<SortCriteria>? sortCriterias)
    {
        if (sortCriterias == null || sortCriterias.Count == 0)
            return source;

        var type = typeof(T);
        IQueryable<T> result = source;

        for (int i = 0; i < sortCriterias.Count; i++)
        {
            var criteria = sortCriterias[i];
            var property = type.GetProperty(criteria.PropertyName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);

            if (property == null)
                continue;

            var parameter = Expression.Parameter(type, "p");
            var propertyAccess = Expression.MakeMemberAccess(parameter, property);
            var orderByExp = Expression.Lambda(propertyAccess, parameter);

            string methodName;
            if (i == 0)
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
        }

        return result;
    }

    public static IQueryable<T> ApplyFiltering<T>(IQueryable<T> source, IReadOnlyList<FilterCriteria>? filters)
    {
        if (filters == null || filters.Count == 0)
            return source;

        var type = typeof(T);
        var result = source;

        foreach (var filter in filters)
        {
            var property = type.GetProperty(filter.PropertyName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
            if (property == null)
                continue;

            var parameter = Expression.Parameter(type, "p");
            var propertyAccess = Expression.MakeMemberAccess(parameter, property);
            var filterExpression = BuildFilterExpression(propertyAccess, filter.Operator, filter.Value, property.PropertyType);

            if (filterExpression == null)
                continue;

            var lambda = Expression.Lambda<Func<T, bool>>(filterExpression, parameter);
            result = result.Where(lambda);
        }

        return result;
    }

    public static IQueryable<T> ApplySearch<T>(IQueryable<T> source, string? searchTerm, IReadOnlyList<string>? searchProperties)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return source;

        var type = typeof(T);
        var parameter = Expression.Parameter(type, "p");

        var properties = searchProperties != null && searchProperties.Count > 0
            ? searchProperties
                .Select(name => type.GetProperty(name, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance))
                .Where(p => p != null && p.PropertyType == typeof(string))
                .Cast<PropertyInfo>()
                .ToList()
            : type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.PropertyType == typeof(string))
                .ToList();

        if (properties.Count == 0)
            return source;

        var containsMethod = typeof(string).GetMethod("Contains", new[] { typeof(string) })!;
        var toLowerMethod = typeof(string).GetMethod("ToLower", Type.EmptyTypes)!;
        var searchValue = Expression.Constant(searchTerm.ToLower());

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
        var query = ApplyFiltering(source, opts.Filters);

        query = ApplySearch(query, opts.SearchTerm, opts.SearchProperties);

        if (opts.SortCriterias != null && opts.SortCriterias.Count > 0)
            query = ApplyMultiSorting(query, opts.SortCriterias);
        else
            query = ApplySorting(query, opts.SortBy, opts.SortDescending);

        return query;
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

    private static Expression? BuildFilterExpression(Expression propertyAccess, FilterOperator op, object? value, Type propertyType)
    {
        if (value is JsonElement nullCandidate && IsJsonNull(nullCandidate))
        {
            if (!AcceptsNull(propertyType))
                return null;

            value = null;
        }

        if (value == null && op != FilterOperator.Equals && op != FilterOperator.NotEquals)
            return null;

        Expression constant;
        if (value == null)
        {
            constant = Expression.Constant(null, propertyType);
        }
        else
        {
            var convertedValue = ConvertValue(value, propertyType);
            if (convertedValue == null)
                return null;
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
                    return null;

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

            return Convert.ChangeType(value, underlyingType);
        }
        catch
        {
            return null;
        }
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
