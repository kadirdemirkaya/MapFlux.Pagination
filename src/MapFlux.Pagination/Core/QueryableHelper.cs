using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using MapFlux.Pagination.Exceptions;
using MapFlux.Pagination.Models;

namespace MapFlux.Pagination.Core;

/// <summary>
/// Builds the filter, search, sort and cursor expressions applied to an <see cref="IQueryable{T}"/>
/// during pagination. The extension methods in <c>MapFlux.Pagination.Extensions</c> compose these.
/// </summary>
public static class QueryableHelper
{
    private const string CursorVersionPrefix = "\u0001v1:";

    private const string CompositeCursorVersionPrefix = "\u0001v2:";

    private const char CompositeCursorLengthSeparator = ':';

    private static readonly HashSet<Type> OperatorComparableTypes = new()
    {
        typeof(byte), typeof(sbyte), typeof(short), typeof(ushort), typeof(int), typeof(uint),
        typeof(long), typeof(ulong), typeof(char), typeof(float), typeof(double), typeof(decimal),
        typeof(DateTime), typeof(DateTimeOffset), typeof(TimeSpan), typeof(DateOnly), typeof(TimeOnly)
    };

    private static readonly MethodInfo StringContainsMethod = typeof(string).GetMethod(nameof(string.Contains), new[] { typeof(string) })!;

    private static readonly MethodInfo StringStartsWithMethod = typeof(string).GetMethod(nameof(string.StartsWith), new[] { typeof(string) })!;

    private static readonly MethodInfo StringEndsWithMethod = typeof(string).GetMethod(nameof(string.EndsWith), new[] { typeof(string) })!;

    private static readonly MethodInfo StringToLowerMethod = typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!;

    private static readonly MethodInfo StringCompareMethod = typeof(string).GetMethod(nameof(string.Compare), new[] { typeof(string), typeof(string) })!;

    private static readonly ConstantExpression NullStringConstant = Expression.Constant(null, typeof(string));

    private static readonly ConstantExpression ZeroConstant = Expression.Constant(0);

    /// <summary>
    /// Orders <paramref name="source"/> by the single property named in <paramref name="sortBy"/>. An
    /// unknown property name is silently ignored and the source is returned unordered.
    /// </summary>
    /// <param name="source">The query to order.</param>
    /// <param name="sortBy">The property name to order by, case-insensitive, or <see langword="null"/>.</param>
    /// <param name="descending">Whether to order descending instead of ascending.</param>
    /// <returns>The ordered query, or <paramref name="source"/> unchanged when <paramref name="sortBy"/> is unset or unknown.</returns>
    public static IQueryable<T> ApplySorting<T>(IQueryable<T> source, string? sortBy, bool descending)
        => ApplySorting(source, sortBy, descending, strict: false);

    /// <summary>
    /// Orders <paramref name="source"/> by the single property named in <paramref name="sortBy"/>.
    /// </summary>
    /// <param name="source">The query to order.</param>
    /// <param name="sortBy">The property name to order by, case-insensitive, or <see langword="null"/>.</param>
    /// <param name="descending">Whether to order descending instead of ascending.</param>
    /// <param name="strict">When <see langword="true"/>, an unknown property name throws <see cref="PaginationStrictModeException"/> instead of being ignored.</param>
    /// <returns>The ordered query, or <paramref name="source"/> unchanged when <paramref name="sortBy"/> is unset.</returns>
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

    /// <summary>
    /// Orders <paramref name="source"/> by each <see cref="SortCriteria"/> in turn, the first as
    /// <c>OrderBy</c>/<c>OrderByDescending</c> and the rest as <c>ThenBy</c>/<c>ThenByDescending</c>.
    /// An unknown property name in a criteria is skipped.
    /// </summary>
    /// <param name="source">The query to order.</param>
    /// <param name="sortCriterias">The ordered list of sort criteria, or <see langword="null"/>.</param>
    /// <returns>The ordered query, or <paramref name="source"/> unchanged when no criteria resolve to a known property.</returns>
    public static IQueryable<T> ApplyMultiSorting<T>(IQueryable<T> source, IReadOnlyList<SortCriteria>? sortCriterias)
        => ApplyMultiSorting(source, sortCriterias, strict: false);

    /// <summary>
    /// Orders <paramref name="source"/> by each <see cref="SortCriteria"/> in turn, the first as
    /// <c>OrderBy</c>/<c>OrderByDescending</c> and the rest as <c>ThenBy</c>/<c>ThenByDescending</c>.
    /// </summary>
    /// <param name="source">The query to order.</param>
    /// <param name="sortCriterias">The ordered list of sort criteria, or <see langword="null"/>.</param>
    /// <param name="strict">When <see langword="true"/>, an unknown property name throws <see cref="PaginationStrictModeException"/> instead of being skipped.</param>
    /// <returns>The ordered query, or <paramref name="source"/> unchanged when no criteria resolve to a known property.</returns>
    public static IQueryable<T> ApplyMultiSorting<T>(IQueryable<T> source, IReadOnlyList<SortCriteria>? sortCriterias, bool strict)
    {
        if (sortCriterias == null || sortCriterias.Count == 0)
            return source;

        var type = typeof(T);
        var metadata = TypeMetadata<T>.Instance;
        IQueryable<T> result = source;
        var hasOrder = false;

        for (int i = 0; i < sortCriterias.Count; i++)
        {
            var criteria = sortCriterias[i];
            var property = metadata.FindProperty(criteria.PropertyName);

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
                TypeMetadata.QueryableOrderMethod(methodName, type, property.PropertyType),
                result.Expression,
                Expression.Quote(orderByExp));

            result = result.Provider.CreateQuery<T>(resultExp);
            hasOrder = true;
        }

        return result;
    }

    /// <summary>
    /// Applies each <see cref="FilterCriteria"/> to <paramref name="source"/> as a <c>Where</c> clause.
    /// An unknown property name, an inconvertible value or an unsupported operator/type combination is
    /// silently dropped and never narrows the result.
    /// </summary>
    /// <param name="source">The query to filter.</param>
    /// <param name="filters">The filters to apply, or <see langword="null"/>.</param>
    /// <returns>The filtered query, or <paramref name="source"/> unchanged when there are no filters.</returns>
    public static IQueryable<T> ApplyFiltering<T>(IQueryable<T> source, IReadOnlyList<FilterCriteria>? filters)
        => ApplyFiltering(source, filters, strict: false);

    /// <summary>
    /// Applies each <see cref="FilterCriteria"/> to <paramref name="source"/> as a <c>Where</c> clause.
    /// </summary>
    /// <param name="source">The query to filter.</param>
    /// <param name="filters">The filters to apply, or <see langword="null"/>.</param>
    /// <param name="strict">When <see langword="true"/>, an unknown property, an inconvertible value or an unsupported operator/type combination throws <see cref="PaginationStrictModeException"/> instead of being dropped.</param>
    /// <returns>The filtered query, or <paramref name="source"/> unchanged when there are no filters.</returns>
    public static IQueryable<T> ApplyFiltering<T>(IQueryable<T> source, IReadOnlyList<FilterCriteria>? filters, bool strict)
    {
        if (filters == null || filters.Count == 0)
            return source;

        var type = typeof(T);
        var metadata = TypeMetadata<T>.Instance;
        var result = source;

        foreach (var filter in filters)
        {
            var property = metadata.FindProperty(filter.PropertyName);
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

    /// <summary>
    /// Filters <paramref name="source"/> to rows where <paramref name="searchTerm"/> case-insensitively
    /// matches any of <paramref name="searchProperties"/>, OR'ed together, or every public <see cref="string"/>
    /// property when none are named.
    /// </summary>
    /// <param name="source">The query to search.</param>
    /// <param name="searchTerm">The term to search for, or <see langword="null"/>/empty to skip searching.</param>
    /// <param name="searchProperties">The <see cref="string"/> property names to search, or <see langword="null"/> to search every public <see cref="string"/> property.</param>
    /// <returns>The filtered query, or <paramref name="source"/> unchanged when there is no search term.</returns>
    public static IQueryable<T> ApplySearch<T>(IQueryable<T> source, string? searchTerm, IReadOnlyList<string>? searchProperties)
        => ApplySearch(source, searchTerm, searchProperties, strict: false);

    /// <summary>
    /// Filters <paramref name="source"/> to rows where <paramref name="searchTerm"/> case-insensitively
    /// matches any of <paramref name="searchProperties"/>, OR'ed together, or every public <see cref="string"/>
    /// property when none are named.
    /// </summary>
    /// <param name="source">The query to search.</param>
    /// <param name="searchTerm">The term to search for, or <see langword="null"/>/empty to skip searching.</param>
    /// <param name="searchProperties">The <see cref="string"/> property names to search, or <see langword="null"/> to search every public <see cref="string"/> property.</param>
    /// <param name="strict">When <see langword="true"/>, an unknown or non-string named property throws <see cref="PaginationStrictModeException"/> instead of being skipped.</param>
    /// <returns>The filtered query, or <paramref name="source"/> unchanged when there is no search term.</returns>
    public static IQueryable<T> ApplySearch<T>(IQueryable<T> source, string? searchTerm, IReadOnlyList<string>? searchProperties, bool strict)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return source;

        var type = typeof(T);
        var metadata = TypeMetadata<T>.Instance;
        var parameter = Expression.Parameter(type, "p");

        IReadOnlyList<PropertyInfo> properties;
        if (searchProperties != null && searchProperties.Count > 0)
        {
            var named = new List<PropertyInfo>(searchProperties.Count);
            foreach (var name in searchProperties)
            {
                var candidate = metadata.FindProperty(name);
                if (candidate == null || candidate.PropertyType != typeof(string))
                {
                    if (strict)
                        throw new PaginationStrictModeException(
                            $"Unknown search property '{name}' on type '{type}'; expected an existing public string property.",
                            name, null, candidate?.PropertyType);

                    continue;
                }

                named.Add(candidate);
            }

            properties = named;
        }
        else
        {
            properties = metadata.StringProperties;
        }

        if (properties.Count == 0)
            return source;

        var searchValue = Expression.Constant(searchTerm.ToLowerInvariant());

        Expression? combinedExpression = null;

        for (int i = 0; i < properties.Count; i++)
        {
            var prop = properties[i];

            if (prop == null) continue;

            var propertyAccess = Expression.MakeMemberAccess(parameter, prop);
            var nullCheck = Expression.NotEqual(propertyAccess, NullStringConstant);
            var toLower = Expression.Call(propertyAccess, StringToLowerMethod);
            var containsCall = Expression.Call(toLower, StringContainsMethod, searchValue);
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

    /// <summary>
    /// Applies the full offset pagination pipeline to <paramref name="source"/>: filter, then search,
    /// then sort (<see cref="PaginationOptions.SortCriterias"/> wins over <see cref="PaginationOptions.SortBy"/>),
    /// falling back to <see cref="ApplyDefaultOrdering{T}"/> when the request produced no ordering.
    /// </summary>
    /// <param name="source">The query to page.</param>
    /// <param name="opts">The pagination options the page is read with.</param>
    /// <returns>The filtered, searched and ordered query.</returns>
    public static IQueryable<T> ApplyFullPipeline<T>(IQueryable<T> source, PaginationOptions opts)
    {
        var query = ApplyFiltering(source, opts.Filters, opts.StrictMode);

        query = ApplySearch(query, opts.SearchTerm, opts.SearchProperties, opts.StrictMode);

        bool ordered;

        if (opts.SortCriterias != null && opts.SortCriterias.Count > 0)
        {
            query = ApplyMultiSorting(query, opts.SortCriterias, opts.StrictMode);
            ordered = HasKnownSortProperty<T>(opts.SortCriterias);
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

    /// <summary>
    /// Orders <paramref name="source"/> by <see cref="PaginationOptions.DefaultSortProperty"/> when set,
    /// otherwise by the resolved key property when <see cref="PaginationOptions.EnsureDeterministicOrder"/>
    /// is set. Called when the requested sort produced no ordering, so an offset page is deterministic.
    /// </summary>
    /// <param name="source">The query to order.</param>
    /// <param name="opts">The pagination options the page is read with.</param>
    /// <returns>The ordered query, or <paramref name="source"/> unchanged when no default ordering applies.</returns>
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

        return TypeMetadata<T>.Instance.FindProperty(propertyName!);
    }

    private static bool HasKnownSortProperty<T>(IReadOnlyList<SortCriteria> sortCriterias)
    {
        for (int i = 0; i < sortCriterias.Count; i++)
        {
            if (FindSortableProperty<T>(sortCriterias[i].PropertyName) != null)
                return true;
        }

        return false;
    }

    private static PropertyInfo? FindKeyProperty<T>()
    {
        var type = typeof(T);

        var annotated = TypeMetadata<T>.Instance.AnnotatedKeyProperty;

        if (annotated != null)
            return annotated;

        return FindSortableProperty<T>("Id") ?? FindSortableProperty<T>($"{type.Name}Id");
    }

    private static IQueryable<T> OrderByProperty<T>(IQueryable<T> source, PropertyInfo property, bool descending)
        => ApplyOrderMethod(source, property, descending ? "OrderByDescending" : "OrderBy");

    private static IQueryable<T> ThenByProperty<T>(IQueryable<T> source, PropertyInfo property, bool descending)
        => ApplyOrderMethod(source, property, descending ? "ThenByDescending" : "ThenBy");

    private static IQueryable<T> ApplyOrderMethod<T>(IQueryable<T> source, PropertyInfo property, string methodName)
    {
        var parameter = Expression.Parameter(typeof(T), "p");
        var propertyAccess = Expression.MakeMemberAccess(parameter, property);
        var keySelector = Expression.Lambda(propertyAccess, parameter);

        var resultExp = Expression.Call(
            TypeMetadata.QueryableOrderMethod(methodName, typeof(T), property.PropertyType),
            source.Expression,
            Expression.Quote(keySelector));

        return source.Provider.CreateQuery<T>(resultExp);
    }

    /// <summary>
    /// Orders <paramref name="source"/> for cursor paging and narrows it to the rows beyond the cursor.
    /// A <see cref="CursorPaginationOptions.Before"/> request is a backward keyset scan: the ordering is
    /// the reverse of the requested one so the rows nearest the cursor are read first, which means the
    /// query yields the page in reverse and the caller has to reverse the materialized rows.
    /// </summary>
    /// <param name="source">The query to page.</param>
    /// <param name="opts">The cursor pagination options the page is read with.</param>
    /// <returns>The ordered and filtered query.</returns>
    public static IQueryable<T> ApplyCursorFilter<T>(IQueryable<T> source, CursorPaginationOptions opts)
        => BuildCursorQuery(source, opts).Query;

    internal static (IQueryable<T> Query, bool Reversed, bool CursorApplied) BuildCursorQuery<T>(
        IQueryable<T> source,
        CursorPaginationOptions opts)
    {
        var cursorProperty = FindSortableProperty<T>(opts.CursorProperty);

        if (cursorProperty == null)
        {
            if (opts.StrictMode)
                throw new PaginationStrictModeException(
                    $"Cursor property '{opts.CursorProperty}' does not exist on type '{typeof(T).Name}'.",
                    opts.CursorProperty,
                    null,
                    typeof(T));

            return (source, false, false);
        }

        var sortProperty = FindSortableProperty<T>(opts.SortBy);
        var backward = string.IsNullOrWhiteSpace(opts.After) && !string.IsNullOrWhiteSpace(opts.Before);
        var cursor = backward ? opts.Before : opts.After;

        Expression<Func<T, bool>>? predicate = null;

        if (!string.IsNullOrWhiteSpace(cursor))
        {
            predicate = BuildCursorPredicate<T>(
                sortProperty,
                cursorProperty,
                cursor!,
                greaterThan: backward ? opts.SortDescending : !opts.SortDescending);

            if (predicate == null && opts.StrictMode)
                throw new PaginationStrictModeException(
                    $"Cursor '{cursor}' cannot be decoded into a value of type '{cursorProperty.PropertyType.Name}' for property '{cursorProperty.Name}'.",
                    cursorProperty.Name,
                    cursor,
                    cursorProperty.PropertyType);
        }

        var reversed = backward && predicate != null;
        var query = ApplyCursorOrdering(source, sortProperty, cursorProperty, opts, reversed);

        if (predicate != null)
            query = query.Where(predicate);

        return (query, reversed, predicate != null);
    }

    /// <summary>
    /// Builds the cursor that points at <paramref name="item"/> for the given options. When
    /// <see cref="CursorPaginationOptions.SortBy"/> names a property other than
    /// <see cref="CursorPaginationOptions.CursorProperty"/>, the cursor carries both the sort key and the
    /// tie-breaking cursor value so the next page continues the keyset without skipping or repeating rows.
    /// </summary>
    /// <param name="item">The row the cursor should point at.</param>
    /// <param name="opts">The cursor pagination options the page was read with.</param>
    /// <returns>The Base64 cursor, or <see langword="null"/> when no cursor can be built for the row.</returns>
    public static string? BuildCursor<T>(T item, CursorPaginationOptions opts)
    {
        if (item == null)
            return null;

        var cursorProperty = FindSortableProperty<T>(opts.CursorProperty);
        var cursorValue = cursorProperty?.GetValue(item);

        if (cursorValue == null)
            return null;

        var sortProperty = FindSortableProperty<T>(opts.SortBy);

        if (sortProperty == null || IsSameProperty(sortProperty, cursorProperty!))
            return EncodeCursor(cursorValue);

        var sortValue = sortProperty.GetValue(item);

        return sortValue == null
            ? EncodeCursor(cursorValue)
            : EncodeCompositeCursor(sortValue, cursorValue);
    }

    private static IQueryable<T> ApplyCursorOrdering<T>(
        IQueryable<T> source,
        PropertyInfo? sortProperty,
        PropertyInfo cursorProperty,
        CursorPaginationOptions opts,
        bool reversed)
    {
        var descending = reversed ? !opts.SortDescending : opts.SortDescending;

        if (sortProperty == null)
            return string.IsNullOrWhiteSpace(opts.SortBy)
                ? OrderByProperty(source, cursorProperty, descending)
                : source;

        if (IsSameProperty(sortProperty, cursorProperty))
            return OrderByProperty(source, cursorProperty, descending);

        return ThenByProperty(
            OrderByProperty(source, sortProperty, descending),
            cursorProperty,
            descending);
    }

    private static bool IsSameProperty(PropertyInfo left, PropertyInfo right)
        => string.Equals(left.Name, right.Name, StringComparison.Ordinal);

    private static Expression<Func<T, bool>>? BuildCursorPredicate<T>(
        PropertyInfo? sortProperty,
        PropertyInfo cursorProperty,
        string cursor,
        bool greaterThan)
    {
        var parameter = Expression.Parameter(typeof(T), "p");

        if (sortProperty != null && !IsSameProperty(sortProperty, cursorProperty))
        {
            var composite = DecodeCompositeCursor(cursor, sortProperty.PropertyType, cursorProperty.PropertyType);

            if (composite != null)
            {
                var sortAccess = Expression.MakeMemberAccess(parameter, sortProperty);
                var sortConstant = Expression.Constant(composite.Value.SortValue, sortProperty.PropertyType);
                var tieAccess = Expression.MakeMemberAccess(parameter, cursorProperty);
                var tieConstant = Expression.Constant(composite.Value.CursorValue, cursorProperty.PropertyType);

                var beyondSortKey = BuildCursorComparison(sortAccess, sortConstant, sortProperty.PropertyType, greaterThan);
                var onSortKey = BuildCursorEquality(sortAccess, sortConstant, sortProperty.PropertyType);
                var beyondTieBreaker = BuildCursorComparison(tieAccess, tieConstant, cursorProperty.PropertyType, greaterThan);

                var keyset = Expression.OrElse(beyondSortKey, Expression.AndAlso(onSortKey, beyondTieBreaker));

                return Expression.Lambda<Func<T, bool>>(keyset, parameter);
            }
        }

        var cursorValue = DecodeCursor(cursor, cursorProperty.PropertyType);
        if (cursorValue == null)
            return null;

        var propertyAccess = Expression.MakeMemberAccess(parameter, cursorProperty);
        var constant = Expression.Constant(cursorValue, cursorProperty.PropertyType);
        var comparison = BuildCursorComparison(propertyAccess, constant, cursorProperty.PropertyType, greaterThan);

        return Expression.Lambda<Func<T, bool>>(comparison, parameter);
    }

    private static Expression BuildCursorComparison(Expression propertyAccess, Expression constant, Type propertyType, bool greaterThan)
    {
        if (propertyType.IsEnum)
        {
            var underlyingType = Enum.GetUnderlyingType(propertyType);
            return CompareWithOperator(
                Expression.Convert(propertyAccess, underlyingType),
                Expression.Convert(constant, underlyingType),
                greaterThan);
        }

        if (propertyType == typeof(string))
            return CompareWithOperator(Expression.Call(StringCompareMethod, propertyAccess, constant), ZeroConstant, greaterThan);

        if (!OperatorComparableTypes.Contains(propertyType))
        {
            var compareTo = TypeMetadata.FindCompareToMethod(propertyType);
            if (compareTo != null)
                return CompareWithOperator(Expression.Call(propertyAccess, compareTo, constant), ZeroConstant, greaterThan);
        }

        return CompareWithOperator(propertyAccess, constant, greaterThan);
    }

    private static Expression BuildCursorEquality(Expression propertyAccess, Expression constant, Type propertyType)
    {
        if (propertyType.IsEnum || propertyType == typeof(string) || OperatorComparableTypes.Contains(propertyType))
            return Expression.Equal(propertyAccess, constant);

        var compareTo = TypeMetadata.FindCompareToMethod(propertyType);

        if (compareTo != null)
            return Expression.Equal(Expression.Call(propertyAccess, compareTo, constant), ZeroConstant);

        return Expression.Equal(propertyAccess, constant);
    }

    private static Expression CompareWithOperator(Expression left, Expression right, bool greaterThan)
        => greaterThan ? Expression.GreaterThan(left, right) : Expression.LessThan(left, right);

    /// <summary>
    /// Encodes a cursor value as Base64. The encoded payload carries a version marker and writes the
    /// value in a round-trippable, culture-independent form, so a cursor produced under one culture
    /// decodes to the same value under any other.
    /// </summary>
    /// <param name="value">The value of the cursor property for the row the cursor points at.</param>
    /// <returns>The Base64 cursor to hand to the client.</returns>
    public static string EncodeCursor(object value)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(CursorVersionPrefix + FormatCursorValue(value));
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// Decodes a Base64 cursor back into a value of <paramref name="targetType"/>. Cursors written by
    /// the versioned format are read invariantly; cursors issued by earlier versions of this package
    /// are still accepted and read the way they were written. For a composite cursor the tie-breaking
    /// cursor value is returned.
    /// </summary>
    /// <param name="cursor">The Base64 cursor received from the client.</param>
    /// <param name="targetType">The type of the cursor property.</param>
    /// <returns>The decoded value, or <see langword="null"/> when the cursor cannot be read.</returns>
    public static object? DecodeCursor(string cursor, Type targetType)
    {
        try
        {
            var bytes = Convert.FromBase64String(cursor);
            var stringValue = System.Text.Encoding.UTF8.GetString(bytes);

            if (stringValue.StartsWith(CursorVersionPrefix, StringComparison.Ordinal))
                return ParseVersionedCursor(stringValue.Substring(CursorVersionPrefix.Length), targetType);

            if (stringValue.StartsWith(CompositeCursorVersionPrefix, StringComparison.Ordinal))
            {
                var parts = SplitCompositeCursor(stringValue);

                return parts == null ? null : ParseVersionedCursor(parts.Value.CursorText, targetType);
            }

            return ParseLegacyCursor(stringValue, targetType);
        }
        catch
        {
            return null;
        }
    }

    private static string EncodeCompositeCursor(object sortValue, object cursorValue)
    {
        var sortText = FormatCursorValue(sortValue);
        var payload = CompositeCursorVersionPrefix
            + sortText.Length.ToString(CultureInfo.InvariantCulture)
            + CompositeCursorLengthSeparator
            + sortText
            + FormatCursorValue(cursorValue);

        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(payload));
    }

    private static (object SortValue, object CursorValue)? DecodeCompositeCursor(string cursor, Type sortType, Type cursorType)
    {
        try
        {
            var bytes = Convert.FromBase64String(cursor);
            var payload = System.Text.Encoding.UTF8.GetString(bytes);
            var parts = SplitCompositeCursor(payload);

            if (parts == null)
                return null;

            var sortValue = ParseVersionedCursor(parts.Value.SortText, sortType);
            var cursorValue = ParseVersionedCursor(parts.Value.CursorText, cursorType);

            if (sortValue == null || cursorValue == null)
                return null;

            return (sortValue, cursorValue);
        }
        catch
        {
            return null;
        }
    }

    private static (string SortText, string CursorText)? SplitCompositeCursor(string payload)
    {
        if (!payload.StartsWith(CompositeCursorVersionPrefix, StringComparison.Ordinal))
            return null;

        var body = payload.Substring(CompositeCursorVersionPrefix.Length);
        var separator = body.IndexOf(CompositeCursorLengthSeparator);

        if (separator <= 0)
            return null;

        if (!int.TryParse(body.Substring(0, separator), NumberStyles.None, CultureInfo.InvariantCulture, out var sortLength))
            return null;

        var values = body.Substring(separator + 1);

        if (sortLength > values.Length)
            return null;

        return (values.Substring(0, sortLength), values.Substring(sortLength));
    }

    private static string FormatCursorValue(object value) => value switch
    {
        DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
        DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O", CultureInfo.InvariantCulture),
        DateOnly dateOnly => dateOnly.ToString("O", CultureInfo.InvariantCulture),
        TimeOnly timeOnly => timeOnly.ToString("O", CultureInfo.InvariantCulture),
        TimeSpan timeSpan => timeSpan.ToString("c", CultureInfo.InvariantCulture),
        double doubleValue => doubleValue.ToString("R", CultureInfo.InvariantCulture),
        float floatValue => floatValue.ToString("R", CultureInfo.InvariantCulture),
        decimal decimalValue => decimalValue.ToString(CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };

    private static object? ParseVersionedCursor(string text, Type targetType)
    {
        if (targetType == typeof(string))
            return text;
        if (targetType == typeof(Guid))
            return Guid.ParseExact(text, "D");
        if (targetType == typeof(DateTime))
            return DateTime.ParseExact(text, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        if (targetType == typeof(DateTimeOffset))
            return DateTimeOffset.ParseExact(text, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        if (targetType == typeof(DateOnly))
            return DateOnly.ParseExact(text, "O", CultureInfo.InvariantCulture);
        if (targetType == typeof(TimeOnly))
            return TimeOnly.ParseExact(text, "O", CultureInfo.InvariantCulture);
        if (targetType == typeof(TimeSpan))
            return TimeSpan.ParseExact(text, "c", CultureInfo.InvariantCulture);
        if (targetType.IsEnum)
            return Enum.Parse(targetType, text, ignoreCase: true);

        return Convert.ChangeType(text, targetType, CultureInfo.InvariantCulture);
    }

    private static object? ParseLegacyCursor(string stringValue, Type targetType)
    {
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

                var method = op switch
                {
                    FilterOperator.Contains => StringContainsMethod,
                    FilterOperator.StartsWith => StringStartsWithMethod,
                    FilterOperator.EndsWith => StringEndsWithMethod,
                    _ => throw new InvalidOperationException()
                };

                var nullCheck = Expression.NotEqual(propertyAccess, NullStringConstant);
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
