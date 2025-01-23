using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace PolyType.SourceGenerator.Helpers;

internal static partial class RoslynHelpers
{
    public static LanguageVersion? GetLanguageVersion(this Compilation compilation) =>
        compilation is CSharpCompilation csc ? csc.LanguageVersion : null;

    public static ITypeSymbol GetMemberType(this ISymbol memberSymbol)
    {
        Debug.Assert(memberSymbol is IPropertySymbol or IFieldSymbol);
        return memberSymbol switch
        {
            IPropertySymbol p => p.Type,
            _ => ((IFieldSymbol)memberSymbol).Type,
        };
    }
    
    /// <summary>
    /// Removes erased compiler metadata such as tuple names and nullable annotations.
    /// </summary>
    public static ITypeSymbol EraseCompilerMetadata(this Compilation compilation, ITypeSymbol type)
    {
        if (type.NullableAnnotation != NullableAnnotation.None)
        {
            type = type.WithNullableAnnotation(NullableAnnotation.None);
        }

        if (type is IArrayTypeSymbol arrayType)
        {
            ITypeSymbol elementType = compilation.EraseCompilerMetadata(arrayType.ElementType);
            return compilation.CreateArrayTypeSymbol(elementType, arrayType.Rank);
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

    public static bool ContainsNullabilityAnnotations(this ITypeSymbol type)
    {
        if (type.NullableAnnotation is NullableAnnotation.Annotated)
        {
            return true;
        }

        if (type is IArrayTypeSymbol arrayType)
        {
            return arrayType.ElementType.ContainsNullabilityAnnotations();
        }
        
        if (type is INamedTypeSymbol namedType)
        {
            if (namedType.ContainingType?.ContainsNullabilityAnnotations() is true)
            {
                return true;
            }

            if (namedType.TypeArguments.Length > 0)
            {
                return namedType.TypeArguments.Any(t => t.ContainsNullabilityAnnotations());
            }
        }

        return false;
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

    public static string GetFullyQualifiedName(this IMethodSymbol methodSymbol)
    {
        Debug.Assert(methodSymbol.IsStatic && methodSymbol.MethodKind is not MethodKind.Constructor);
        return $"{methodSymbol.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}.{methodSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}";
    }

    public static bool IsGenericTypeDefinition(this ITypeSymbol type) =>
        type is INamedTypeSymbol { IsGenericType: true, IsDefinition: true };

    // Gets all type arguments, including the ones specified by containing types in order of nesting.
    public static ITypeSymbol[] GetRecursiveTypeArguments(this ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol { IsGenericType: true } namedType)
        {
            return [];
        }

        List<ITypeSymbol> typeArguments = [];
        GetAllTypeArgumentsCore(namedType);
        return typeArguments.ToArray();

        void GetAllTypeArgumentsCore(INamedTypeSymbol type)
        {
            if (!type.IsGenericType)
            {
                return;
            }
            
            if (type.ContainingType is { } containingType)
            {
                GetAllTypeArgumentsCore(containingType);
            }
            
            typeArguments.AddRange(type.TypeArguments);
        }
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

            ITypeSymbol[] args = remainingTypeArgs[.. typeDefinition.Arity].ToArray();
            remainingTypeArgs = remainingTypeArgs[typeDefinition.Arity ..];
            return typeDefinition.Construct(args);
        }
    }

    public static IPropertySymbol GetBaseProperty(this IPropertySymbol property)
    {
        while (property.OverriddenProperty is { } baseProp)
        {
            property = baseProp;
        }

        return property;
    }

    public static bool MatchesNamespace(this ISymbol? symbol, ImmutableArray<string> namespaceTokens)
    {
        for (int i = namespaceTokens.Length - 1; i >= 0; i--)
        {
            if (symbol?.Name != namespaceTokens[i])
            {
                return false;
            }

            symbol = symbol.ContainingNamespace;
        }

        return symbol is null or INamespaceSymbol { IsGlobalNamespace: true };
    }

    /// <summary>
    /// Returns a string representation of the type suitable for use as an identifier in source code or file names.
    /// </summary>
    public static string CreateTypeIdentifier(this ITypeSymbol type, ReadOnlySpan<string> reservedIdentifiers, bool includeNamespaces = false)
    {
        StringBuilder sb = new();
        GenerateCore(type, sb);
        string identifier = sb.ToString();

        // Do not return identifiers that are C# keywords or reserved identifiers.
        return IsCSharpKeyword(identifier) || reservedIdentifiers.IndexOf(identifier) >= 0 
            ? "__Type_" + identifier 
            : identifier;

        void GenerateCore(ITypeSymbol type, StringBuilder sb)
        {
            switch (type)
            {
                case ITypeParameterSymbol typeParameter:
                    sb.Append(typeParameter.Name);
                    break;

                case IArrayTypeSymbol arrayType:
                    GenerateCore(arrayType.ElementType, sb);
                    sb.Append("_Array");
                    if (arrayType.Rank > 1)
                    {
                        // _Array2D, _Array3D, etc.
                        sb.Append(arrayType.Rank);
                        sb.Append('D');
                    }
                    break;

                case INamedTypeSymbol namedType:
                    if (includeNamespaces)
                    {
                        PrependNamespaces(namedType.ContainingNamespace);
                        PrependContainingTypes(namedType);
                    }

                    sb.Append(namedType.Name);

                    IEnumerable<ITypeSymbol> typeArguments = namedType.IsTupleType
                        ? namedType.TupleElements.Select(e => e.Type)
                        : namedType.TypeArguments;

                    foreach (ITypeSymbol argument in namedType.TypeArguments)
                    {
                        sb.Append('_');
                        GenerateCore(argument, sb);
                    }

                    break;

                default:
                    Debug.Fail($"Type {type} not supported");
                    throw new InvalidOperationException();
            }

            void PrependNamespaces(INamespaceSymbol ns)
            {
                if (ns.ContainingNamespace is { } containingNs)
                {
                    PrependNamespaces(containingNs);
                    sb.Append(ns.Name);
                    sb.Append('_');
                }
            }

            void PrependContainingTypes(INamedTypeSymbol namedType)
            {
                if (namedType.ContainingType is { } parent)
                {
                    PrependContainingTypes(parent);
                    GenerateCore(parent, sb);
                    sb.Append('_');
                }
            }
        }
    }

    public static bool IsCSharpKeyword(string name) =>
        SyntaxFacts.GetKeywordKind(name) is not SyntaxKind.None ||
        SyntaxFacts.GetContextualKeywordKind(name) is not SyntaxKind.None;

    public static string EscapeKeywordIdentifier(string name) =>
        IsCSharpKeyword(name) ? "@" + name : name;

    public static Location? GetLocation(this AttributeData attributeData)
    {
        SyntaxReference? asr = attributeData.ApplicationSyntaxReference;
        return asr?.SyntaxTree.GetLocation(asr.Span);
    }

    public static AttributeData? GetAttribute(this ISymbol symbol, INamedTypeSymbol? attributeType, bool inherit = true)
    {
        if (attributeType is null)
        {
            return null;
        }

        AttributeData? attribute = symbol.GetAttributes()
            .FirstOrDefault(ad => SymbolEqualityComparer.Default.Equals(ad.AttributeClass, attributeType));

        if (attribute is null && inherit)
        {
            Debug.Assert(attribute is not IEventSymbol);

            if (symbol is IPropertySymbol { OverriddenProperty: { } baseProperty })
            {
                return baseProperty.GetAttribute(attributeType, inherit: true);
            }

            if (symbol is ITypeSymbol { BaseType: { } baseType })
            {
                return baseType.GetAttribute(attributeType, inherit);
            }

            if (symbol is IMethodSymbol { OverriddenMethod: { } baseMethod })
            {
                return baseMethod.GetAttribute(attributeType, inherit: true);
            }
        }

        return attribute;
    }

    public static bool HasAttribute(this ISymbol symbol, INamedTypeSymbol? attributeType)
        => attributeType != null && symbol.GetAttributes().Any(ad => SymbolEqualityComparer.Default.Equals(ad.AttributeClass, attributeType));

    public static bool TryGetNamedArgument<T>(this AttributeData attributeData, string name, [MaybeNullWhen(false)] out T? result)
    {
        foreach (KeyValuePair<string, TypedConstant> namedArg in attributeData.NamedArguments)
        {
            if (namedArg.Key == name)
            {
                result = (T)namedArg.Value.Value!;
                return true;
            }
        }

        result = default;
        return false;
    }

    /// <summary>
    /// Returns the kind keyword corresponding to the specified declaration syntax node.
    /// </summary>
    public static string GetTypeKindKeyword(this BaseTypeDeclarationSyntax typeDeclaration)
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
