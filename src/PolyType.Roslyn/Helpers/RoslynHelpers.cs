﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;

namespace PolyType.Roslyn.Helpers;

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

    public static IEnumerable<IMethodSymbol> GetConstructors(this INamedTypeSymbol type) =>
        type.Constructors.Where(ctor => ctor is { IsStatic: false, DeclaredAccessibility: Accessibility.Public });

    public static IEnumerable<IMethodSymbol> GetMethods(this INamedTypeSymbol type, string name, bool isStatic) =>
         type.GetMembers(name)
            .OfType<IMethodSymbol>()
            .Where(method => method.IsStatic == isStatic && method.DeclaredAccessibility == Accessibility.Public);

    public static bool ContainsLocation(this Compilation compilation, Location location) =>
        location.SourceTree != null && compilation.ContainsSyntaxTree(location.SourceTree);

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

    public static string GetFullyQualifiedName(this ITypeSymbol typeSymbol) =>
        typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    public static bool IsGenericTypeDefinition(this ITypeSymbol typeSymbol) =>
        typeSymbol is INamedTypeSymbol { IsGenericType: true, IsDefinition: true };

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

    public static IEnumerable<IMethodSymbol> GetCollectionBuilderAttributeMethods(
        this Compilation compilation,
        INamedTypeSymbol type,
        ITypeSymbol elementType,
        CancellationToken cancellationToken)
    {
        AttributeData? attributeData = type.GetAttributes().FirstOrDefault(static attr =>
            attr is { AttributeClass.Name: "CollectionBuilderAttribute" } &&
            attr.AttributeClass.ContainingNamespace.ToDisplayString() == "System.Runtime.CompilerServices");

        if (!TryParseAttributeData(attributeData, out INamedTypeSymbol? builderType, out string? methodName))
        {
            yield break;
        }

        var cmp = SymbolEqualityComparer.Default;
        foreach (IMethodSymbol method in builderType.GetMembers().OfType<IMethodSymbol>())
        {
            if (!method.IsStatic || method.Name != methodName)
            {
                continue;
            }

            if (method.IsGenericMethod)
            {
                if (method.TypeArguments.Length != 1)
                {
                    continue;
                }

                yield return method.Construct(elementType);
            }
            else
            {
                yield return method;
            }
        }

        bool TryParseAttributeData(
            AttributeData? attributeData,
            [NotNullWhen(true)] out INamedTypeSymbol? builderType,
            [NotNullWhen(true)] out string? builderMethod)
        {
            builderType = null;
            builderMethod = null;

            if (attributeData is null)
            {
                return false;
            }

            if (attributeData.AttributeClass is { TypeKind: TypeKind.Error })
            {
                // In certain cases, the attribute class may not be resolved because it might have been polyfilled
                // by a source generator such as PolySharp. In such cases, parse attribute data from the syntax trees manually.
                if (attributeData.ApplicationSyntaxReference?.GetSyntax(cancellationToken) is AttributeSyntax attrSyntax &&
                    attrSyntax is { ArgumentList.Arguments: { Count: 2 } arguments, SyntaxTree: { } syntaxTree })
                {
                    SemanticModel semanticModel = compilation.GetSemanticModel(syntaxTree);
                    foreach (AttributeArgumentSyntax attrArgumentSyntax in arguments)
                    {
                        switch (attrArgumentSyntax.Expression)
                        {
                            case TypeOfExpressionSyntax typeOfExpr:
                                builderType = semanticModel.GetTypeInfo(typeOfExpr.Type, cancellationToken).Type as INamedTypeSymbol;
                                break;

                            case LiteralExpressionSyntax literalExpr when literalExpr.IsKind(SyntaxKind.StringLiteralExpression):
                                builderMethod = literalExpr.Token.ValueText;
                                break;

                            case InvocationExpressionSyntax
                            {
                                Expression: IdentifierNameSyntax { Identifier.Text: "nameof" },
                                ArgumentList.Arguments: [ArgumentSyntax argument]
                            }:
                                switch (argument.Expression)
                                {
                                    case MemberAccessExpressionSyntax memberAccessExpr:
                                        builderMethod = memberAccessExpr.Name.Identifier.Text;
                                        break;

                                    case IdentifierNameSyntax identifierName:
                                        builderMethod = identifierName.Identifier.Text;
                                        break;
                                }

                                break;
                        }
                    }
                }
            }
            else if (
                attributeData.ConstructorArguments.Length == 2 &&
                attributeData.ConstructorArguments[0].Value is INamedTypeSymbol typeParam &&
                attributeData.ConstructorArguments[1].Value is string methodName)
            {
                builderType = typeParam;
                builderMethod = methodName;
            }

            return builderType is { IsGenericType: false } && builderMethod is not null;
        }
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
            null => parameter.Type.IsNullable() ? "null!" : "default",
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

    // Applies the type arguments to the type, working recursively on container types that may also be generic.
    // Returns null if there is a mismatch between the number of parameters and the combined arity of the generic type.
    public static INamedTypeSymbol? ConstructRecursive(this INamedTypeSymbol typeDefinition, ReadOnlySpan<ITypeSymbol> typeArguments)
    {
        INamedTypeSymbol? result = ConstructRecursiveCore(typeDefinition, ref typeArguments);
        return typeArguments.IsEmpty ? result : null;

        static INamedTypeSymbol? ConstructRecursiveCore(INamedTypeSymbol typeDefinition, ref ReadOnlySpan<ITypeSymbol> remainingTypeArgs)
        {
            Debug.Assert(typeDefinition.IsGenericTypeDefinition());

            if (typeDefinition.ContainingType?.IsGenericTypeDefinition() is true)
            {
                INamedTypeSymbol? specializedContainingType = ConstructRecursiveCore(typeDefinition.ContainingType, ref remainingTypeArgs);
                if (specializedContainingType is null)
                {
                    return null;
                }

                typeDefinition = specializedContainingType.GetTypeMembers().First(t => t.Name == typeDefinition.Name && t.Arity == typeDefinition.Arity);
            }

            if (remainingTypeArgs.Length < typeDefinition.Arity)
            {
                return null;
            }

            if (typeDefinition.Arity is 0)
            {
                return typeDefinition;
            }

            ITypeSymbol[] args = remainingTypeArgs[..typeDefinition.Arity].ToArray();
            remainingTypeArgs = remainingTypeArgs[typeDefinition.Arity..];
            return typeDefinition.Construct(args);
        }
    }

    // Gets all type arguments, including the ones specified by containing types in order of nesting.
    public static ITypeSymbol[] GetRecursiveTypeArguments(this INamedTypeSymbol type)
    {
        List<ITypeSymbol> typeArguments = [];
        GetAllTypeArgumentsCore(type);
        return typeArguments.ToArray();

        void GetAllTypeArgumentsCore(INamedTypeSymbol type)
        {
            if (!type.IsGenericType)
            {
                return;
            }

            if (type.ContainingType is { IsGenericType: true } containingType)
            {
                GetAllTypeArgumentsCore(containingType);
            }

            typeArguments.AddRange(type.TypeArguments);
        }
    }
}
