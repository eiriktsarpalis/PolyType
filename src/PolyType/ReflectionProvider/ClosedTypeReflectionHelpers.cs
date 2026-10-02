using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace PolyType.ReflectionProvider;

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
internal static class ClosedTypeReflectionHelpers
{
    private const string ClosedTypeAttributeName = "System.Runtime.CompilerServices.IsClosedTypeAttribute";

    public static bool IsClosedType(Type type) => GetClosedTypeAttribute(type) is not null;

    public static IEnumerable<Type> GetClosedDerivedTypes(Type type)
    {
        HashSet<Type> path = [];
        return Expand(type);

        IEnumerable<Type> Expand(Type closedType)
        {
            Type definition = GetDefinition(closedType);
            if (!path.Add(definition))
            {
                throw new InvalidOperationException($"Closed type '{type}' contains cyclic derived-type metadata.");
            }

            CustomAttributeData attribute = GetClosedTypeAttribute(definition)
                ?? throw new InvalidOperationException($"Type '{definition}' is not a closed class.");

            foreach (Type derivedType in GetDeclaredDerivedTypes(definition, attribute))
            {
                if (IsClosedType(GetDefinition(derivedType)))
                {
                    foreach (Type terminalType in Expand(derivedType))
                    {
                        yield return terminalType;
                    }
                }
                else
                {
                    yield return derivedType;
                }
            }

            path.Remove(definition);
        }
    }

    public static bool IsAtLeastAsVisibleAs(Type type, Type baseType) =>
        TypeVisibilityHelpers.IsAtLeastAsVisibleAs(
            type,
            baseType,
            GetAccessibility,
            static type => type.DeclaringType,
            static (from, to) => HasInternalAccessTo(from.Assembly, to.Assembly),
            IsAccessibleViaInheritance,
            static (left, right) => GetDefinition(left) == GetDefinition(right));

    private static CustomAttributeData? GetClosedTypeAttribute(Type type) =>
        type is { IsClass: true, IsAbstract: true } &&
        type.GetCustomAttributesData().FirstOrDefault(attribute => attribute.AttributeType.FullName == ClosedTypeAttributeName) is { } attribute
            ? attribute
            : null;

    private static IEnumerable<Type> GetDeclaredDerivedTypes(Type type, CustomAttributeData attribute)
    {
        foreach (CustomAttributeNamedArgument argument in attribute.NamedArguments)
        {
            if (argument.MemberName != "DerivedTypes")
            {
                continue;
            }

            // Mono can materialize the array directly instead of wrapping each element.
            if (argument.TypedValue.Value is Type[] materializedTypes)
            {
                foreach (Type? derivedType in materializedTypes)
                {
                    yield return derivedType
                        ?? throw new InvalidOperationException($"Closed type '{type}' contains invalid derived-type metadata.");
                }
            }
            else if (argument.TypedValue.Value is IList<CustomAttributeTypedArgument> arguments)
            {
                foreach (CustomAttributeTypedArgument derivedType in arguments)
                {
                    yield return derivedType.Value as Type
                        ?? throw new InvalidOperationException($"Closed type '{type}' contains invalid derived-type metadata.");
                }
            }
            else
            {
                throw new InvalidOperationException($"Closed type '{type}' contains invalid derived-type metadata.");
            }

            yield break;
        }
    }

    private static TypeAccessibility GetAccessibility(Type type) => type switch
    {
        { IsPublic: true } or { IsNestedPublic: true } => TypeAccessibility.Public,
        { IsNestedFamORAssem: true } => TypeAccessibility.ProtectedOrInternal,
        { IsNestedFamily: true } => TypeAccessibility.Protected,
        { IsNestedFamANDAssem: true } => TypeAccessibility.ProtectedAndInternal,
        { IsNestedPrivate: true } => TypeAccessibility.Private,
        _ => TypeAccessibility.Internal,
    };

    private static bool IsAccessibleViaInheritance(Type baseType, Type derivedType)
    {
        Type baseDefinition = GetDefinition(baseType);
        for (Type? current = derivedType; current is not null; current = current.BaseType)
        {
            if (GetDefinition(current) == baseDefinition)
            {
                return true;
            }
        }

        return baseDefinition.IsInterface &&
            derivedType.GetInterfaces().Any(type => GetDefinition(type) == baseDefinition);
    }

    private static bool HasInternalAccessTo(Assembly from, Assembly to)
    {
        if (from == to)
        {
            return true;
        }

        string? fromName = from.GetName().Name;
        foreach (CustomAttributeData attribute in to.GetCustomAttributesData())
        {
            if (attribute.AttributeType == typeof(InternalsVisibleToAttribute) &&
                attribute.ConstructorArguments is [{ Value: string friendName }, ..])
            {
                string friendSimpleName = friendName.Split(',')[0].Trim();
                if (string.Equals(friendSimpleName, fromName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static Type GetDefinition(Type type) => type.IsGenericType ? type.GetGenericTypeDefinition() : type;
}
