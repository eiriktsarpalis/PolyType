﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;

namespace TypeShape.Roslyn.Helpers;

internal static class RoslynHelpers
{
    public static bool IsNullable(this ITypeSymbol type)
        => !type.IsValueType || type.OriginalDefinition.SpecialType is SpecialType.System_Nullable_T;

    public static bool IsNonNullableAnnotation(this IParameterSymbol parameter)
        => IsParameterNonNullable(parameter, parameter.Type);

    public static IPropertySymbol GetBaseProperty(this IPropertySymbol property)
    {
        while (property.OverriddenProperty is { } baseProp)
        {
            property = baseProp;
        }

        return property;
    }

    public static void ResolveNullableAnnotation(this ISymbol member, out bool isGetterNonNullable, out bool isSetterNonNullable)
    {
        Debug.Assert(member is IFieldSymbol or IPropertySymbol);

        isGetterNonNullable = false;
        isSetterNonNullable = false;

        if (member is IFieldSymbol field)
        {
            isGetterNonNullable = IsReturnTypeNonNullable(field, field.Type);
            isSetterNonNullable = IsParameterNonNullable(field, field.Type);
        }
        else if (member is IPropertySymbol property)
        {
            Debug.Assert(!property.IsIndexer);

            if (property.OverriddenProperty is { } baseProp && (property.GetMethod is null || property.SetMethod is null))
            {
                // We are handling a property that potentially overrides only part of the base signature.
                // Resolve the annotations of the base property first before looking at the derived ones.
                baseProp.ResolveNullableAnnotation(out isGetterNonNullable, out isSetterNonNullable);
            }

            if (property.GetMethod != null)
            {
                isGetterNonNullable = IsReturnTypeNonNullable(property, property.Type);
            }

            if (property.SetMethod != null)
            {
                isSetterNonNullable = IsParameterNonNullable(property, property.Type);
            }
        }
    }

    private static bool IsReturnTypeNonNullable(ISymbol symbol, ITypeSymbol returnType)
    {
        if (!returnType.IsNullable())
        {
            return true;
        }

        if (symbol.HasCodeAnalysisAttribute("MaybeNullAttribute"))
        {
            return false;
        }

        if (symbol.HasCodeAnalysisAttribute("NotNullAttribute"))
        {
            return true;
        }

        return returnType.NullableAnnotation is NullableAnnotation.NotAnnotated;
    }

    private static bool IsParameterNonNullable(ISymbol symbol, ITypeSymbol parameterType)
    {
        if (!parameterType.IsNullable())
        {
            return true;
        }

        if (symbol.HasCodeAnalysisAttribute("AllowNullAttribute"))
        {
            return false;
        }

        if (symbol.HasCodeAnalysisAttribute("DisallowNullAttribute"))
        {
            return true;
        }

        return parameterType.NullableAnnotation is NullableAnnotation.NotAnnotated;
    }

    private static bool HasCodeAnalysisAttribute(this ISymbol symbol, string attributeName)
    {
        return symbol.GetAttributes().Any(attr =>
            attr.AttributeClass?.Name == attributeName &&
            attr.AttributeClass.ContainingNamespace.ToDisplayString() == "System.Diagnostics.CodeAnalysis");
    }

    public static bool IsRequired(this IPropertySymbol property)
    {
#if ROSLYN4_4_OR_GREATER
        return property.IsRequired;
#else
        return s_IsRequiredProperty?.Invoke(property) ?? false;
#endif
    }

    public static bool IsRequired(this IFieldSymbol fieldInfo)
    {
#if ROSLYN4_4_OR_GREATER
        return fieldInfo.IsRequired;
#else
        return s_IsRequiredField?.Invoke(fieldInfo) ?? false;
#endif
    }

#if !ROSLYN4_4_OR_GREATER
    private static readonly Func<IPropertySymbol, bool>? s_IsRequiredProperty = CreatePropertyAccessor<IPropertySymbol, bool>(typeof(IPropertySymbol).GetProperty("IsRequired"));
    private static readonly Func<IFieldSymbol, bool>? s_IsRequiredField = CreatePropertyAccessor<IFieldSymbol, bool>(typeof(IFieldSymbol).GetProperty("IsRequired"));

    private static Func<T, TProperty>? CreatePropertyAccessor<T, TProperty>(PropertyInfo? propertyInfo)
    {
        return propertyInfo?.GetMethod is null ? null : (Func<T, TProperty>)Delegate.CreateDelegate(typeof(Func<T, TProperty>), propertyInfo.GetMethod);
    }
#endif

    public static string GetFullyQualifiedName(this ITypeSymbol typeSymbol)
        => typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    public static bool IsGenericTypeDefinition(this ITypeSymbol typeSymbol)
        => typeSymbol is INamedTypeSymbol { IsGenericType: true } namedTy && 
           SymbolEqualityComparer.Default.Equals(namedTy.OriginalDefinition, typeSymbol);

    public static bool ContainsGenericParameters(this ITypeSymbol typeSymbol)
    {
        if (typeSymbol.TypeKind is TypeKind.TypeParameter or TypeKind.Error)
        {
            return true;
        }

        if (typeSymbol is INamedTypeSymbol namedTypeSymbol)
        {
            if (namedTypeSymbol.IsUnboundGenericType)
            {
                return true;
            }

            for (; namedTypeSymbol != null; namedTypeSymbol = namedTypeSymbol.ContainingType)
            {
                if (namedTypeSymbol.TypeArguments.Any(arg => arg.ContainsGenericParameters()))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static IPropertySymbol[]? GetClassTupleProperties(IAssemblySymbol coreLibAssembly, INamedTypeSymbol typeSymbol)
    {
        if (!IsClassTupleType(typeSymbol))
        {
            return null;
        }

        var elementList = new List<IPropertySymbol>();
        while (true)
        {
            IEnumerable<IPropertySymbol> itemProperties = typeSymbol.GetMembers()
                .OfType<IPropertySymbol>()
                .Where(prop => !prop.IsStatic && prop.Name.StartsWith("Item", StringComparison.Ordinal));

            elementList.AddRange(itemProperties);

            if (typeSymbol.TypeArguments.Length < 8)
            {
                // Tuple is without a nested component.
                break;
            }

            if (typeSymbol.TypeArguments[7] is INamedTypeSymbol restType && IsClassTupleType(restType))
            {
                typeSymbol = restType;
            }
            else
            {
                // Non-standard nested tuple representation -- treat as usual class.
                return null;
            }
        }

        return elementList.ToArray();

        bool IsClassTupleType(INamedTypeSymbol type) =>
            type is
            {
                IsGenericType: true,
                IsValueType: false,
                Name: "Tuple",
                ContainingNamespace.Name: "System"
            } &&
            SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, coreLibAssembly);
    }

    public static bool HasSetsRequiredMembersAttribute(this IMethodSymbol constructor)
    {
        return constructor.MethodKind is MethodKind.Constructor &&
            constructor.GetAttributes().Any(attr => 
                attr.AttributeClass is { Name: "SetsRequiredMembersAttribute", ContainingNamespace: INamespaceSymbol ns } && 
                ns.ToDisplayString() == "System.Diagnostics.CodeAnalysis");
    }

    public static bool TryGetCollectionBuilderAttribute(this INamedTypeSymbol type, ITypeSymbol elementType, [NotNullWhen(true)] out IMethodSymbol? builderMethod)
    {
        builderMethod = null;
        AttributeData? attributeData = type.GetAttributes().FirstOrDefault(attr => 
            attr.AttributeClass?.Name == "CollectionBuilderAttribute" &&
            attr.AttributeClass.ContainingNamespace.ToDisplayString() == "System.Runtime.CompilerServices");

        if (attributeData is null)
        {
            return false;
        }

        INamedTypeSymbol builderType = (INamedTypeSymbol)attributeData.ConstructorArguments[0].Value!;
        string methodName = (string)attributeData.ConstructorArguments[1].Value!;

        if (builderType.IsGenericType)
        {
            return false;
        }

        var cmp = SymbolEqualityComparer.Default;
        foreach (IMethodSymbol method in builderType.GetMembers().OfType<IMethodSymbol>())
        {
            if (method.IsStatic && method.Name == methodName && method.Parameters is [{ Type: INamedTypeSymbol parameterType }] &&
                parameterType.IsGenericType && parameterType.ConstructedFrom.Name is "ReadOnlySpan")
            {
                ITypeSymbol spanElementType = parameterType.TypeArguments[0];
                if (cmp.Equals(spanElementType, elementType) && cmp.Equals(method.ReturnType, type))
                {
                    builderMethod = method;
                    return true;
                }

                if (method.IsGenericMethod && method.TypeArguments is [ITypeSymbol typeParameter] &&
                    cmp.Equals(spanElementType, typeParameter))
                {
                    IMethodSymbol specializedMethod = method.Construct(elementType);
                    if (cmp.Equals(specializedMethod.ReturnType, type))
                    {
                        builderMethod = specializedMethod;
                        // Continue searching since we prefer non-generic methods.
                    }
                }
            }
        }

        return builderMethod != null;
    }

    /// <summary>
    /// Get a location object that doesn't capture a reference to Compilation.
    /// </summary>
    public static Location GetLocationTrimmed(this Location location)
    {
        return Location.Create(location.SourceTree?.FilePath ?? string.Empty, location.SourceSpan, location.GetLineSpan().Span);
    }

    public static ICollection<ITypeSymbol> GetSortedTypeHierarchy(this ITypeSymbol type)
    {
        if (type.TypeKind != TypeKind.Interface)
        {
            var list = new List<ITypeSymbol>();
            for (ITypeSymbol? current = type; current != null; current = current.BaseType)
            {
                list.Add(current);
            }

            return list;
        }
        else
        {
            // Interface hierarchies support multiple inheritance.
            // For consistency with class hierarchy resolution order,
            // sort topologically from most derived to least derived.
            return CommonHelpers.TraverseGraphWithTopologicalSort<ITypeSymbol>(type, static t => t.AllInterfaces, SymbolEqualityComparer.Default);
        }
    }

    public static IEnumerable<ISymbol> GetAllMembers(this ITypeSymbol type)
        => type.GetSortedTypeHierarchy().SelectMany(t => t.GetMembers());

    public static bool IsAssignableFrom([NotNullWhen(true)] this ITypeSymbol? baseType, [NotNullWhen(true)] ITypeSymbol? type)
    {
        if (baseType is null || type is null)
        {
            return false;
        }

        SymbolEqualityComparer comparer = SymbolEqualityComparer.Default;

        for (ITypeSymbol? current = type; current != null; current = current.BaseType)
        {
            if (comparer.Equals(current, baseType))
            {
                return true;
            }
        }

        foreach (INamedTypeSymbol @interface in type.AllInterfaces)
        {
            if (comparer.Equals(@interface, baseType))
            {
                return true;
            }
        }

        return false;
    }

    public static INamedTypeSymbol? GetCompatibleGenericBaseType(this ITypeSymbol type, [NotNullWhen(true)] INamedTypeSymbol? genericType)
    {
        if (genericType is null)
        {
            return null;
        }

        Debug.Assert(genericType.IsGenericTypeDefinition());

        if (genericType.TypeKind is TypeKind.Interface)
        {
            foreach (INamedTypeSymbol interfaceType in type.AllInterfaces)
            {
                if (IsMatchingGenericType(interfaceType, genericType))
                {
                    return interfaceType;
                }
            }
        }

        for (INamedTypeSymbol? current = type as INamedTypeSymbol; current != null; current = current.BaseType)
        {
            if (IsMatchingGenericType(current, genericType))
            {
                return current;
            }
        }

        return null;

        static bool IsMatchingGenericType(INamedTypeSymbol candidate, INamedTypeSymbol baseType)
        {
            return candidate.IsGenericType && SymbolEqualityComparer.Default.Equals(candidate.ConstructedFrom, baseType);
        }
    }

    public static IMethodSymbol? GetMethodSymbol(this ITypeSymbol? type, Func<IMethodSymbol, bool> predicate)
    {
        if (type is null)
        {
            return null;
        }

        return type.GetMembers()
            .OfType<IMethodSymbol>()
            .FirstOrDefault(predicate);
    }

    public static IMethodSymbol? MakeGenericMethod(this IMethodSymbol? method, params ITypeSymbol[] arguments)
    {
        if (method is null)
        {
            return null;
        }

        return method.Construct(arguments);
    }

    public static string? FormatDefaultValueExpr(this IParameterSymbol parameter)
    {
        if (!parameter.HasExplicitDefaultValue)
        {
            return null;
        }

        string literalExpr = parameter.ExplicitDefaultValue switch
        {
            null => "null!",
            false => "false",
            true => "true",

            string s => SymbolDisplay.FormatLiteral(s, quote: true),
            char c => SymbolDisplay.FormatLiteral(c, quote: true),

            double.NaN => "double.NaN",
            double.NegativeInfinity => "double.NegativeInfinity",
            double.PositiveInfinity => "double.PositiveInfinity",
            double d => $"{d.ToString("G17", CultureInfo.InvariantCulture)}d",

            float.NaN => "float.NaN",
            float.NegativeInfinity => "float.NegativeInfinity",
            float.PositiveInfinity => "float.PositiveInfinity",
            float f => $"{f.ToString("G9", CultureInfo.InvariantCulture)}f",

            decimal d => $"{d.ToString(CultureInfo.InvariantCulture)}m",

            // Must be one of the other numeric types or an enum
            object num => Convert.ToString(num, CultureInfo.InvariantCulture),
        };

        bool requiresCast = parameter.Type 
            is { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T }
            or { TypeKind: TypeKind.Enum };

        return requiresCast
            ? $"({parameter.Type.GetFullyQualifiedName()}){literalExpr}"
            : literalExpr;
    }
}
