using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Reflection;

namespace MapFlux.Pagination.Core;

internal sealed class TypeMetadata
{
    private const BindingFlags LookupFlags = BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance;

    private const BindingFlags EnumerationFlags = BindingFlags.Public | BindingFlags.Instance;

    private static readonly ConcurrentDictionary<Type, TypeMetadata> Cache = new();

    private static readonly ConcurrentDictionary<Type, MethodInfo?> CompareToMethods = new();

    private static readonly ConcurrentDictionary<string, MethodInfo> OpenQueryableOrderMethods = new();

    private static readonly ConcurrentDictionary<(string Name, Type EntityType, Type KeyType), MethodInfo> QueryableOrderMethods = new();

    private readonly Type _type;

    private readonly Dictionary<string, PropertyInfo> _propertiesByName;

    private readonly HashSet<string>? _caseInsensitiveCollisions;

    private TypeMetadata(Type type)
    {
        _type = type;

        var properties = type.GetProperties(EnumerationFlags);

        _propertiesByName = new Dictionary<string, PropertyInfo>(properties.Length, StringComparer.OrdinalIgnoreCase);
        HashSet<string>? collisions = null;
        PropertyInfo? annotatedKey = null;
        var stringProperties = new List<PropertyInfo>();

        foreach (var property in properties)
        {
            if (property.PropertyType == typeof(string))
                stringProperties.Add(property);

            if (annotatedKey == null && property.CanRead && property.IsDefined(typeof(KeyAttribute), inherit: true))
                annotatedKey = property;

            if (!_propertiesByName.TryGetValue(property.Name, out var existing))
            {
                _propertiesByName.Add(property.Name, property);
                continue;
            }

            var winner = MoreDerived(existing, property);

            if (winner == null)
            {
                collisions ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                collisions.Add(property.Name);
                continue;
            }

            _propertiesByName[property.Name] = winner;
        }

        _caseInsensitiveCollisions = collisions;
        AnnotatedKeyProperty = annotatedKey;
        StringProperties = stringProperties.ToArray();
    }

    internal PropertyInfo[] StringProperties { get; }

    internal PropertyInfo? AnnotatedKeyProperty { get; }

    internal static TypeMetadata For(Type type)
        => Cache.GetOrAdd(type, static key => new TypeMetadata(key));

    internal PropertyInfo? FindProperty(string name)
    {
        if (name == null)
            throw new ArgumentNullException(nameof(name));

        if (_caseInsensitiveCollisions != null && _caseInsensitiveCollisions.Contains(name))
            return _type.GetProperty(name, LookupFlags);

        return _propertiesByName.TryGetValue(name, out var property) ? property : null;
    }

    internal static MethodInfo? FindCompareToMethod(Type propertyType)
        => CompareToMethods.GetOrAdd(propertyType, static type =>
        {
            var method = type.GetMethod(nameof(IComparable.CompareTo), new[] { type });
            return method != null && method.ReturnType == typeof(int) ? method : null;
        });

    internal static MethodInfo QueryableOrderMethod(string name, Type entityType, Type keyType)
        => QueryableOrderMethods.GetOrAdd(
            (name, entityType, keyType),
            static key => OpenQueryableOrderMethod(key.Name).MakeGenericMethod(key.EntityType, key.KeyType));

    private static MethodInfo OpenQueryableOrderMethod(string name)
        => OpenQueryableOrderMethods.GetOrAdd(name, static methodName => typeof(Queryable)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(candidate =>
                candidate.Name == methodName
                && candidate.IsGenericMethodDefinition
                && candidate.GetGenericArguments().Length == 2
                && IsKeySelectorOverload(candidate.GetParameters())));

    private static bool IsKeySelectorOverload(ParameterInfo[] parameters)
        => parameters.Length == 2
            && parameters[1].ParameterType.IsGenericType
            && parameters[1].ParameterType.GetGenericTypeDefinition() == typeof(Expression<>);

    private static PropertyInfo? MoreDerived(PropertyInfo left, PropertyInfo right)
    {
        var leftType = left.DeclaringType;
        var rightType = right.DeclaringType;

        if (leftType == null || rightType == null || leftType == rightType)
            return null;

        if (leftType.IsAssignableFrom(rightType))
            return right;

        if (rightType.IsAssignableFrom(leftType))
            return left;

        return null;
    }
}

internal static class TypeMetadata<T>
{
    internal static readonly TypeMetadata Instance = TypeMetadata.For(typeof(T));
}
