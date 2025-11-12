using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
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

    public static ITypeSymbol GetReturnType(this IMethodSymbol method) =>
        method is { MethodKind: MethodKind.Constructor, IsStatic: false } ? method.ContainingType : method.ReturnType;

    /// <summary>
    /// Returns the runtime <see cref="Type.ToString()"/> value of the specified type symbol.
    /// </summary>
    public static string GetReflectionToStringName(this ITypeSymbol type)
    {
        StringBuilder builder = new StringBuilder();
        FormatType(type, builder);
        return builder.ToString();

        static void FormatType(ITypeSymbol type, StringBuilder builder)
        {
            if (type is IArrayTypeSymbol arrayType)
            {
                FormatType(arrayType.ElementType, builder);
                builder.Append('[');
                builder.Append(',', arrayType.Rank - 1);
                builder.Append(']');
                return;
            }

            if (type is IPointerTypeSymbol pointerType)
            {
                FormatType(pointerType.PointedAtType, builder);
                builder.Append('*');
                return;
            }

            if (type is ITypeParameterSymbol typeParam)
            {
                builder.Append(typeParam.Name);
                return;
            }

            Debug.Assert(type is INamedTypeSymbol);
            List<ITypeSymbol>? aggregateGenericParams = null;
            var namedType = (INamedTypeSymbol)type;
            FormatNamedType(namedType, builder, ref aggregateGenericParams);

            // Append the aggregate generic parameters for all nested types at the end.
            if (aggregateGenericParams is not null)
            {
                builder.Append('[');
                foreach (ITypeSymbol typeArg in aggregateGenericParams)
                {
                    FormatType(typeArg, builder);
                    builder.Append(',');
                }

                builder.Length--; // remove last comma
                builder.Append(']');
            }
        }

        static void FormatNamedType(INamedTypeSymbol namedType, StringBuilder builder, ref List<ITypeSymbol>? aggregateGenericParams)
        {
            if (namedType.ContainingType is { } containingType)
            {
                FormatNamedType(containingType, builder, ref aggregateGenericParams);
                builder.Append('+');
            }
            else
            {
                FormatNamespace(namedType.ContainingNamespace, builder);
            }

            builder.Append(namedType.Name);
            if (namedType.TypeArguments.Length > 0)
            {
                builder.Append('`');
                builder.Append(namedType.TypeArguments.Length);
                (aggregateGenericParams ??= []).AddRange(namedType.TypeArguments);
            }
        }

        static void FormatNamespace(INamespaceSymbol? namespaceSymbol, StringBuilder builder)
        {
            if (namespaceSymbol is null or { IsGlobalNamespace: true })
            {
                return;
            }

            FormatNamespace(namespaceSymbol.ContainingNamespace, builder);
            builder.Append(namespaceSymbol.Name);
            builder.Append('.');
        }
    }

    /// <summary>
    /// Removes erased compiler metadata such as tuple names and nullable annotations.
    /// </summary>
    public static ITypeSymbol EraseCompilerMetadata(this Compilation compilation, ITypeSymbol type, bool useForSymbolDisplayOnly = false)
    {
        if (useForSymbolDisplayOnly && !SymbolDisplayRequiresErasure(type))
        {
            return type;
        }

        return EraseCore(type);

        ITypeSymbol EraseCore(ITypeSymbol type)
        {
            if (type.NullableAnnotation is not NullableAnnotation.None)
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
                if (namedType is { IsTupleType: true, TupleElements.Length: >= 2 })
                {
                    ImmutableArray<ITypeSymbol> erasedElements = namedType.TupleElements
                        .Select(e => compilation.EraseCompilerMetadata(e.Type))
                        .ToImmutableArray();

                    type = compilation.CreateTupleTypeSymbol(erasedElements);
                }
                else if (namedType.IsGenericType)
                {
                    ImmutableArray<ITypeSymbol> typeArguments = namedType.TypeArguments;
                    INamedTypeSymbol? containingType = namedType.ContainingType;

                    if (containingType?.IsGenericType is true)
                    {
                        containingType = (INamedTypeSymbol)EraseCore(containingType);
                        type = namedType = containingType.GetTypeMembers().First(t => t.Name == namedType.Name && t.Arity == namedType.Arity);
                    }

                    if (typeArguments.Length > 0)
                    {
                        ITypeSymbol[] erasedTypeArgs = typeArguments
                            .Select(EraseCore)
                            .ToArray();

                        type = namedType.ConstructedFrom.Construct(erasedTypeArgs);
                    }
                }
            }

            return type;
        }

        static bool SymbolDisplayRequiresErasure(ITypeSymbol type)
        {
            // No need to check for type.NullableAnnotation since SymbolDisplayFormat.FullyQualifiedFormat
            // does not enable IncludeNullableReferenceTypeModifier.

            if (type is IArrayTypeSymbol arrayType)
            {
                return SymbolDisplayRequiresErasure(arrayType.ElementType);
            }
            if (type is INamedTypeSymbol namedType)
            {
                if (namedType.IsTupleType)
                {
                    foreach (IFieldSymbol e in namedType.TupleElements)
                    {
                        if (!string.IsNullOrEmpty(e.Name) || SymbolDisplayRequiresErasure(e.Type))
                        {
                            return true;
                        }
                    }
                }
                else if (namedType.TypeArguments.Length > 0)
                {
                    foreach (ITypeSymbol t in namedType.TypeArguments)
                    {
                        if (SymbolDisplayRequiresErasure(t))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
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

    public static string GetFullyQualifiedName(this ISymbol symbol)
    {
        Debug.Assert(symbol is ITypeSymbol or ({ IsStatic: true } and (IMethodSymbol or IPropertySymbol or IFieldSymbol)));
        return symbol is ITypeSymbol typeSymbol
            ? typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            : $"{symbol.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}.{symbol.Name}";
    }

    /// <summary>
    /// Returns a name suitable for auto-deriving DerivedTypeShapeAttribute.Name that includes type arguments separated by underscores.
    /// </summary>
    /// <remarks>
    /// Examples:
    /// - Cow&lt;SolidHoof&gt; → Cow_SolidHoof
    /// - Cow&lt;List&lt;SolidHoof&gt;&gt; → Cow_List_SolidHoof
    /// </remarks>
    public static string GetDerivedTypeShapeName(this ITypeSymbol type)
    {
        StringBuilder builder = new StringBuilder();
        ITypeSymbol? skipContainingType = (type as INamedTypeSymbol)?.ContainingType;
        FormatTypeWithUnderscores(type, builder, skipContainingType);
        return builder.ToString();

        static void FormatTypeWithUnderscores(ITypeSymbol type, StringBuilder builder, ITypeSymbol? skipContainingType)
        {
            if (type is IArrayTypeSymbol arrayType)
            {
                builder.Append("Array");
                if (arrayType.Rank > 1)
                {
                    builder.Append(arrayType.Rank);
                    builder.Append('D');
                }

                builder.Append('_');
                FormatTypeWithUnderscores(arrayType.ElementType, builder, skipContainingType);
                return;
            }

            if (type is IPointerTypeSymbol pointerType)
            {
                FormatTypeWithUnderscores(pointerType.PointedAtType, builder, skipContainingType);
                builder.Append("Pointer");
                return;
            }

            if (type is ITypeParameterSymbol typeParam)
            {
                builder.Append(typeParam.Name);
                return;
            }

            Debug.Assert(type is INamedTypeSymbol);
            var namedType = (INamedTypeSymbol)type;
            
            // For nested types, include containing type unless it matches the skip type
            if (namedType.ContainingType is { } containingType &&
                !SymbolEqualityComparer.Default.Equals(containingType, skipContainingType))
            {
                FormatTypeWithUnderscores(containingType, builder, skipContainingType);
                builder.Append('_');
            }

            builder.Append(namedType.Name);

            // Append type arguments separated by underscores
            if (namedType.TypeArguments.Length > 0)
            {
                foreach (ITypeSymbol typeArg in namedType.TypeArguments)
                {
                    builder.Append('_');
                    FormatTypeWithUnderscores(typeArg, builder, skipContainingType);
                }
            }
        }
    }

    public static IEnumerable<TMember> GetMembers<TMember>(this ITypeSymbol type, string name) where TMember : ISymbol
        => type.GetMembers(name).OfType<TMember>();

    public static bool MatchesNamespace(this INamespaceSymbol? symbol, ImmutableArray<string> namespaceTokens)
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
                    // Use prefix notation to avoid ambiguity: Array_Int32 instead of Int32_Array
                    // This prevents collisions like Dictionary<string, int[]> vs Dictionary<string, int>[]
                    sb.Append("Array");
                    if (arrayType.Rank > 1)
                    {
                        // Array2D, Array3D, etc.
                        sb.Append(arrayType.Rank);
                        sb.Append('D');
                    }
                    sb.Append('_');
                    GenerateCore(arrayType.ElementType, sb);
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

                    foreach (ITypeSymbol argument in typeArguments)
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

    public static IEnumerable<(AttributeData Attribute, bool IsInherited)> GetAllAttributes(this ISymbol symbol)
    {
        foreach (AttributeData attr in symbol.GetAttributes())
        {
            yield return (attr, IsInherited: false);
        }

        IEnumerable<(AttributeData, bool)> baseAttrs = symbol switch
        {
            ITypeSymbol { BaseType: { } baseType } => baseType.GetAllAttributes(),
            IPropertySymbol { OverriddenProperty: { } baseProperty } => baseProperty.GetAllAttributes(),
            IMethodSymbol { OverriddenMethod: { } baseMethod } => baseMethod.GetAllAttributes(),
            IEventSymbol { OverriddenEvent: { } baseEvent } => baseEvent.GetAllAttributes(),
            _ => [],
        };

        foreach ((AttributeData attr, _) in baseAttrs)
        {
            yield return (attr, IsInherited: true);
        }
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

            if (symbol is IEventSymbol { OverriddenEvent: { } baseEvent })
            {
                return baseEvent.GetAttribute(attributeType, inherit: true);
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

    public static bool TryGetNamedArguments(this AttributeData attributeData, string name, out ImmutableArray<TypedConstant> result)
    {
        foreach (KeyValuePair<string, TypedConstant> namedArg in attributeData.NamedArguments)
        {
            if (namedArg.Key == name)
            {
                result = namedArg.Value.Values!;
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

    public static string FormatTypedConstant(this Compilation compilation, ISymbol context, TypedConstant constant)
    {
        switch (constant.Kind)
        {
            case TypedConstantKind.Primitive:
                return FormatPrimitive(constant.Value);

            case TypedConstantKind.Enum:
                return FormatEnum(constant.Value, constant.Type);

            case TypedConstantKind.Type:
                var type = (ITypeSymbol)constant.Value!;
                if (compilation.IsSymbolAccessibleWithin(type, context))
                {
                    return $"typeof({type.GetFullyQualifiedName()})";
                }

                string assemblyQualifiedName = type.GetAssemblyQualifiedName();
                return $"global::System.Type.GetType({SymbolDisplay.FormatLiteral(assemblyQualifiedName, quote: true)})!";

            case TypedConstantKind.Array:
                return FormatArray(constant.Values, constant.Type);

            default:
                return "default";
        }

        static string EscapeChar(char c)
        {
            return c switch
            {
                '\'' => "\\'",
                '\\' => "\\\\",
                '\0' => "\\0",
                '\a' => "\\a",
                '\b' => "\\b",
                '\f' => "\\f",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                '\v' => "\\v",
                _ => c.ToString()
            };
        }

        static string FormatEnum(object? value, ITypeSymbol? type)
        {
            if (value is null || type is not INamedTypeSymbol enumType)
            {
                return "null";
            }

            return $"({enumType.GetFullyQualifiedName()}){value}";
        }

        string FormatArray(ImmutableArray<TypedConstant> values, ITypeSymbol? arrayType)
        {
            Debug.Assert(arrayType is IArrayTypeSymbol);

            if (values.IsDefaultOrEmpty)
            {
                var elementType = ((IArrayTypeSymbol)arrayType!).ElementType;
                return $"new {elementType.GetFullyQualifiedName()}[] {{ }}";
            }

            string items = string.Join(", ", values.Select(tc => FormatTypedConstant(compilation, context, tc)));
            return $"new[] {{ {items} }}";
        }

        static string FormatPrimitive(object? value)
        {
            return value switch
            {
                null => "null",
                string str => SymbolDisplay.FormatLiteral(str, quote: true),
                bool boolValue => boolValue ? "true" : "false",
                char charValue => $"'{EscapeChar(charValue)}'",
                byte byteValue => $"(byte){byteValue}",
                sbyte sbyteValue => $"(sbyte){sbyteValue}",
                short shortValue => $"(short){shortValue}",
                ushort ushortValue => $"(ushort){ushortValue}",
                int intValue => intValue.ToString(CultureInfo.InvariantCulture),
                uint uintValue => $"{uintValue.ToString(CultureInfo.InvariantCulture)}u",
                long longValue => $"{longValue.ToString(CultureInfo.InvariantCulture)}L",
                ulong ulongValue => $"{ulongValue.ToString(CultureInfo.InvariantCulture)}UL",
                float floatValue => floatValue.ToString("R", CultureInfo.InvariantCulture) + "f",
                double doubleValue => doubleValue.ToString("R", CultureInfo.InvariantCulture) + "d",
                decimal decimalValue => decimalValue.ToString(CultureInfo.InvariantCulture) + "m",
                _ => value.ToString() ?? "null"
            };
        }
    }

    /// <summary>
    /// Returns the runtime <see cref="Type.AssemblyQualifiedName"/> value of the specified type symbol.
    /// </summary>
    public static string GetAssemblyQualifiedName(this ITypeSymbol type)
    {
        StringBuilder builder = new();
        FormatType(type, builder);
        return builder.ToString();

        static void FormatType(ITypeSymbol type, StringBuilder builder, bool appendAssemblyIdentity = true)
        {
            switch (type)
            {
                case IArrayTypeSymbol arrayType:
                    FormatType(arrayType.ElementType, builder, appendAssemblyIdentity: false);
                    builder.Append('[');
                    builder.Append(',', arrayType.Rank - 1);
                    builder.Append(']');
                    break;

                case IPointerTypeSymbol pointerType:
                    FormatType(pointerType.PointedAtType, builder, appendAssemblyIdentity: false);
                    builder.Append('*');
                    break;

                case ITypeParameterSymbol typeParam:
                    // Open generic parameter (only appears for unbound generic types).
                    builder.Append(typeParam.Name);
                    return;

                case INamedTypeSymbol namedType:
                    List<ITypeSymbol>? aggregateGenericParams = null;
                    FormatNamedType(namedType, builder, ref aggregateGenericParams);

                    // Only emit concrete generic arguments if the type is a constructed generic (no type parameters).
                    if (aggregateGenericParams is not null &&
                        aggregateGenericParams.Count > 0 &&
                        aggregateGenericParams.TrueForAll(tp => tp is not ITypeParameterSymbol))
                    {
                        builder.Append('[');
                        foreach (ITypeSymbol tp in aggregateGenericParams)
                        {
                            builder.Append('[');
                            FormatType(tp, builder);
                            builder.Append(']');
                            builder.Append(',');
                        }

                        builder.Length -= 1; // remove last comma
                        builder.Append(']');
                    }
                    break;

                default:
                    Debug.Fail($"Unsupported type symbol: {type}");
                    break;
            }

            if (appendAssemblyIdentity && type.ContainingAssembly is not null)
            {
                builder.Append(", ");
                FormatAssemblyIdentity(type.ContainingAssembly, builder);
            }
        }

        static void FormatNamedType(INamedTypeSymbol namedType, StringBuilder builder, ref List<ITypeSymbol>? aggregateGenericParams)
        {
            if (namedType.ContainingType is { } containingType)
            {
                FormatNamedType(containingType, builder, ref aggregateGenericParams);
                builder.Append('+');
            }
            else
            {
                FormatNamespace(namedType.ContainingNamespace, builder);
            }

            builder.Append(namedType.Name);

            // Backtick + arity for generic types (both open and constructed).
            if (namedType.TypeArguments.Length > 0)
            {
                builder.Append('`');
                builder.Append(namedType.TypeArguments.Length);
                // Collect only if constructed (arguments may still be type parameters; filtering happens later).
                (aggregateGenericParams ??= new()).AddRange(namedType.TypeArguments);
            }
        }

        static void FormatNamespace(INamespaceSymbol? ns, StringBuilder builder)
        {
            if (ns is null || ns.IsGlobalNamespace)
            {
                return;
            }

            FormatNamespace(ns.ContainingNamespace, builder);
            builder.Append(ns.Name);
            builder.Append('.');
        }

        static void FormatAssemblyIdentity(IAssemblySymbol assembly, StringBuilder builder)
        {
            AssemblyIdentity id = assembly.Identity;
            builder.Append(id.Name);
            builder.Append(", Version=");
            builder.Append(id.Version);
            builder.Append(", Culture=");
            builder.Append(string.IsNullOrEmpty(id.CultureName) ? "neutral" : id.CultureName);
            builder.Append(", PublicKeyToken=");
            if (id.PublicKeyToken.IsDefaultOrEmpty)
            {
                builder.Append("null");
            }
            else
            {
                foreach (byte b in id.PublicKeyToken)
                {
                    builder.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                }
            }
        }
    }
}
