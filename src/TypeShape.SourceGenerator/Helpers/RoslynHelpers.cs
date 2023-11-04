﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace TypeShape.SourceGenerator.Helpers;

internal static class RoslynHelpers
{
    public static bool IsNullable(this ITypeSymbol type)
        => !type.IsValueType || type.OriginalDefinition.SpecialType is SpecialType.System_Nullable_T;

    public static bool IsNonNullableAnnotation(this ITypeSymbol type)
        => !type.IsNullable() || type.NullableAnnotation is NullableAnnotation.NotAnnotated;

    public static bool IsNonNullableAnnotation(this IParameterSymbol parameter)
        => IsParameterNonNullable(parameter, parameter.Type);

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

    public static ITypeSymbol EraseCompilerMetadata(this Compilation compilation, ITypeSymbol type)
    {
        if (type.NullableAnnotation != NullableAnnotation.None)
        {
            type = type.WithNullableAnnotation(NullableAnnotation.None);
        }

        if (type is INamedTypeSymbol namedType)
        {
            if (namedType.IsTupleType)
            {
                if (namedType.TupleElements.Length < 2)
                {
                    return type;
                }

                ImmutableArray<ITypeSymbol> erasedElements = namedType.TupleElements
                    .Select(e => compilation.EraseCompilerMetadata(e.Type))
                    .ToImmutableArray();

                type = compilation.CreateTupleTypeSymbol(erasedElements);
            }
            else if (namedType.IsGenericType)
            {
                ImmutableArray<ITypeSymbol> typeArguments = namedType.TypeArguments;
                INamedTypeSymbol? containingType = namedType.ContainingType;

                if (containingType?.IsGenericType == true)
                {
                    containingType = (INamedTypeSymbol)compilation.EraseCompilerMetadata(containingType);
                    type = namedType = containingType.GetTypeMembers().First(t => t.Name == namedType.Name && t.Arity == namedType.Arity);
                }

                if (typeArguments.Length > 0)
                {
                    ITypeSymbol[] erasedTypeArgs = typeArguments
                        .Select(compilation.EraseCompilerMetadata)
                        .ToArray();

                    type = namedType.ConstructedFrom.Construct(erasedTypeArgs);
                }
            }
        }

        return type;
    }

    /// <summary>
    /// this.QualifiedNameOnly = containingSymbol.QualifiedNameOnly + "." + this.Name
    /// </summary>
    public static SymbolDisplayFormat QualifiedNameOnlyFormat { get; } = 
        new SymbolDisplayFormat(
            globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            memberOptions: SymbolDisplayMemberOptions.IncludeContainingType);

    public static string GetFullyQualifiedName(this ITypeSymbol typeSymbol)
        => typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    public static bool IsGenericTypeDefinition(this ITypeSymbol typeSymbol)
        => typeSymbol is INamedTypeSymbol { IsGenericType: true } namedTy && 
           SymbolEqualityComparer.Default.Equals(namedTy.OriginalDefinition, typeSymbol);

    public static string GetGeneratedPropertyName(this ITypeSymbol typeSymbol)
    {
        switch (typeSymbol)
        { 
            case IArrayTypeSymbol arrayTypeSymbol:
                int rank = arrayTypeSymbol.Rank;
                string suffix = rank == 1 ? "_Array" : $"_Array{rank}D"; // Array, Array2D, Array3D, ...
                return arrayTypeSymbol.ElementType.GetGeneratedPropertyName() + suffix;

            case INamedTypeSymbol namedType when (namedType.IsTupleType):
                {
                    StringBuilder sb = new();

                    sb.Append(namedType.Name);

                    foreach (IFieldSymbol element in namedType.TupleElements)
                    {
                        sb.Append('_');
                        sb.Append(element.Type.GetGeneratedPropertyName());
                    }

                    return sb.ToString();
                }

            case INamedTypeSymbol namedType:
                {
                    if (namedType.TypeArguments.Length == 0 && namedType.ContainingType is null)
                        return namedType.Name;

                    StringBuilder sb = new();

                    PrependContainingTypes(namedType);

                    sb.Append(namedType.Name);

                    foreach (ITypeSymbol argument in namedType.TypeArguments)
                    {
                        sb.Append('_');
                        sb.Append(argument.GetGeneratedPropertyName());
                    }

                    return sb.ToString();

                    void PrependContainingTypes(INamedTypeSymbol namedType)
                    {
                        if (namedType.ContainingType is { } parent)
                        {
                            PrependContainingTypes(parent);
                            sb.Append(parent.GetGeneratedPropertyName());
                            sb.Append('_');
                        }
                    }
                }

            default:
                Debug.Fail($"Type {typeSymbol} not supported");
                return null!;
        }
    }

    public static bool ContainsGenericParameters(this ITypeSymbol typeSymbol)
    {
        if (typeSymbol.TypeKind is TypeKind.TypeParameter or TypeKind.Error)
            return true;

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

    public static bool IsNonTrivialValueTupleType(this ITypeSymbol typeSymbol)
    {
        return typeSymbol.IsTupleType && typeSymbol is INamedTypeSymbol ts && ts.TupleElements.Length > 1;
    }

    public static ITypeSymbol[]? GetClassTupleElements(this Compilation compilation, IAssemblySymbol coreLibAssembly, ITypeSymbol typeSymbol)
    {
        if (!IsClassTupleType(typeSymbol))
        {
            return null;
        }

        var elementList = new List<ITypeSymbol>();
        while (true)
        {
            var arguments = ((INamedTypeSymbol)typeSymbol).TypeArguments;

            if (arguments.Length < 8)
            {
                elementList.AddRange(arguments);
                break;
            }
            else if (IsClassTupleType(arguments[7]))
            {
                elementList.AddRange(arguments.Take(7));
                typeSymbol = arguments[7];
            }
            else
            {
                // Non-standard nested tuple representation -- treat as usual class.
                return null;
            }
        }

        return elementList.ToArray();

        bool IsClassTupleType(ITypeSymbol type) =>
            type is INamedTypeSymbol
            {
                IsGenericType: true,
                IsValueType: false,
                Name: "Tuple",
                ContainingNamespace.Name: "System"
            } &&
            SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, coreLibAssembly);
    }

    public static IEnumerable<IFieldSymbol> GetTupleElementsWithoutLabels(this INamedTypeSymbol tuple)
    {
        Debug.Assert(tuple.IsTupleType);

        foreach (IFieldSymbol element in tuple.TupleElements)
        {
            yield return element.IsExplicitlyNamedTupleElement ? element.CorrespondingTupleField! : element;
        }
    }

    public static bool IsAutoProperty(this IPropertySymbol property)
    {
        return property.ContainingType.GetMembers()
            .OfType<IFieldSymbol>()
            .Any(field => SymbolEqualityComparer.Default.Equals(field.AssociatedSymbol, property));
    }

    public static bool HasSetsRequiredMembersAttribute(this IMethodSymbol constructor)
    {
        return constructor.MethodKind is MethodKind.Constructor &&
            constructor.GetAttributes().Any(attr => 
                attr.AttributeClass is { Name: "SetsRequiredMembersAttribute", ContainingNamespace: INamespaceSymbol ns } && 
                ns.ToDisplayString() == "System.Diagnostics.CodeAnalysis");
    }

    /// <summary>
    /// Get a location object that doesn't capture a reference to Compilation.
    /// </summary>
    public static Location GetLocationTrimmed(this Location location)
    {
        return Location.Create(location.SourceTree?.FilePath ?? string.Empty, location.SourceSpan, location.GetLineSpan().Span);
    }

    public static Location? GetLocation(this AttributeData attributeData)
    {
        SyntaxReference? asr = attributeData.ApplicationSyntaxReference;
        return asr?.SyntaxTree.GetLocation(asr.Span);
    }

    public static ITypeSymbol[] GetSortedTypeHierarchy(this ITypeSymbol type)
    {
        if (type.TypeKind != TypeKind.Interface)
        {
            var list = new List<ITypeSymbol>();
            for (ITypeSymbol? current = type; current != null; current = current.BaseType)
            {
                list.Add(current);
            }

            return list.ToArray();
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

    /// <summary>
    /// An "atomic value" in this context defines a type that is either 
    /// a primitive, string or a self-contained value like decimal, DateTime or Uri.
    /// </summary>
    public static bool IsAtomicValueType(this Compilation compilation, IAssemblySymbol coreLibAssembly, ITypeSymbol type)
    {
        switch (type.SpecialType)
        {
            // Primitive types
            case SpecialType.System_Boolean:
            case SpecialType.System_Char:
            case SpecialType.System_SByte:
            case SpecialType.System_Byte:
            case SpecialType.System_Int16:
            case SpecialType.System_UInt16:
            case SpecialType.System_Int32:
            case SpecialType.System_UInt32:
            case SpecialType.System_Int64:
            case SpecialType.System_UInt64:
            case SpecialType.System_Single:
            case SpecialType.System_Double:
            // CoreLib non-primitives that represent a single value.
            case SpecialType.System_String:
            case SpecialType.System_Decimal:
            case SpecialType.System_DateTime:
            // Include System.Object since it doesn't contain any data statically.
            case SpecialType.System_Object:
                return true;
        }

        if (!SymbolEqualityComparer.Default.Equals(coreLibAssembly, type.ContainingAssembly))
        {
            return false;
        }

        switch (type.ToDisplayString())
        {
            case "System.Half":
            case "System.Int128":
            case "System.IntU128":
            case "System.Guid":
            case "System.DateTimeOffset":
            case "System.DateOnly":
            case "System.TimeSpan":
            case "System.TimeOnly":
            case "System.Version":
            case "System.Uri":
            case "System.Text.Rune":
                return true;
        }

        return false;
    }

    /// <summary>
    /// Returns the kind keyword corresponding to the specified declaration syntax node.
    /// </summary>
    public static string GetTypeKindKeyword(this TypeDeclarationSyntax typeDeclaration)
    {
        switch (typeDeclaration.Kind())
        {
            case SyntaxKind.ClassDeclaration:
                return "class";
            case SyntaxKind.InterfaceDeclaration:
                return "interface";
            case SyntaxKind.StructDeclaration:
                return "struct";
            case SyntaxKind.RecordDeclaration:
                return "record";
            case SyntaxKind.RecordStructDeclaration:
                return "record struct";
            case SyntaxKind.EnumDeclaration:
                return "enum";
            case SyntaxKind.DelegateDeclaration:
                return "delegate";
            default:
                Debug.Fail("unexpected syntax kind");
                return null!;
        }
    }
}
