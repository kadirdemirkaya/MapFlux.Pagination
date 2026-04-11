using System.Linq.Expressions;
using System.Reflection;

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
}
