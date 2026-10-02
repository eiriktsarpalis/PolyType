using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace PolyType.Roslyn.Helpers;

internal static class ClosedTypeHelpers
{
    private const string AttributeNamespace = "System.Runtime.CompilerServices";
    private const string AttributeName = "IsClosedTypeAttribute";
    private static readonly Func<ITypeSymbol, bool>? s_isClosedAccessor = CreateIsClosedAccessor();
    private static readonly Func<ISymbol, int>? s_metadataTokenAccessor = CreateMetadataTokenAccessor();

    public static bool IsClosedType(this INamedTypeSymbol type)
    {
        if (type is not { TypeKind: TypeKind.Class, IsAbstract: true })
        {
            return false;
        }

        if (TryGetMetadata(type.OriginalDefinition, out MetadataReader? reader, out TypeDefinitionHandle handle))
        {
            return !MetadataHelpers.GetAttribute(reader, reader.GetTypeDefinition(handle).GetCustomAttributes(), AttributeNamespace, AttributeName).IsNil;
        }

        if (s_isClosedAccessor is not null)
        {
            return s_isClosedAccessor(type);
        }

        return type.DeclaringSyntaxReferences.Any(reference =>
            reference.GetSyntax() is BaseTypeDeclarationSyntax declaration &&
            declaration.Modifiers.Any(modifier => modifier.Text is "closed"));
    }

    public static ImmutableArray<INamedTypeSymbol> GetClosedDerivedTypes(
        this INamedTypeSymbol type,
        Compilation compilation,
        CancellationToken cancellationToken)
    {
        var result = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
        HashSet<INamedTypeSymbol> path = new(SymbolEqualityComparer.Default);
        Dictionary<IModuleSymbol, INamedTypeSymbol[]> sourceTypes = new(SymbolEqualityComparer.Default);
        Expand(type.OriginalDefinition);
        return result.ToImmutable();

        void Expand(INamedTypeSymbol closedType)
        {
            cancellationToken.ThrowIfCancellationRequested();
            closedType = closedType.OriginalDefinition;
            if (!path.Add(closedType))
            {
                throw new BadImageFormatException($"Closed type '{type}' contains cyclic derived-type metadata.");
            }

            IEnumerable<INamedTypeSymbol> declaredTypes;
            if (TryGetMetadata(closedType, out MetadataReader? reader, out TypeDefinitionHandle handle))
            {
                declaredTypes = ReadDeclaredDerivedTypes(closedType, compilation, reader, handle);
            }
            else
            {
                if (!sourceTypes.TryGetValue(closedType.ContainingModule, out INamedTypeSymbol[]? candidates))
                {
                    // Match the compiler's pre-order module traversal, including nested types.
                    sourceTypes.Add(closedType.ContainingModule, candidates = EnumerateNamedTypes(closedType.ContainingModule.GlobalNamespace).ToArray());
                }

                declaredTypes = candidates.Where(candidate =>
                    SymbolEqualityComparer.Default.Equals(candidate.BaseType?.OriginalDefinition, closedType));
            }

            foreach (INamedTypeSymbol candidate in declaredTypes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                INamedTypeSymbol definition = candidate.OriginalDefinition;
                if (definition.IsClosedType())
                {
                    Expand(definition);
                }
                else
                {
                    result.Add(definition.IsGenericType ? definition.ConstructUnboundGenericType() : definition);
                }
            }

            path.Remove(closedType);
        }
    }

    public static bool IsAtLeastAsVisibleAs(this INamedTypeSymbol type, INamedTypeSymbol baseType) =>
        TypeVisibilityHelpers.IsAtLeastAsVisibleAs(
            type,
            baseType,
            static type => type.DeclaredAccessibility switch
            {
                Accessibility.Private => TypeAccessibility.Private,
                Accessibility.ProtectedAndInternal => TypeAccessibility.ProtectedAndInternal,
                Accessibility.Protected => TypeAccessibility.Protected,
                Accessibility.Internal => TypeAccessibility.Internal,
                Accessibility.ProtectedOrInternal => TypeAccessibility.ProtectedOrInternal,
                _ => TypeAccessibility.Public,
            },
            static type => type.ContainingType,
            static (from, to) => SymbolEqualityComparer.Default.Equals(from.ContainingAssembly, to.ContainingAssembly) ||
                to.ContainingAssembly.GivesAccessTo(from.ContainingAssembly),
            IsAccessibleViaInheritance,
            static (left, right) => SymbolEqualityComparer.Default.Equals(left.OriginalDefinition, right.OriginalDefinition));

    private static Func<ITypeSymbol, bool>? CreateIsClosedAccessor()
    {
        MethodInfo? getter = typeof(ITypeSymbol).GetProperty("IsClosed")?.GetMethod;
        return getter is null ? null : (Func<ITypeSymbol, bool>)getter.CreateDelegate(typeof(Func<ITypeSymbol, bool>));
    }

    private static Func<ISymbol, int>? CreateMetadataTokenAccessor()
    {
        MethodInfo? getter = typeof(ISymbol).GetProperty("MetadataToken")?.GetMethod;
        return getter is null ? null : (Func<ISymbol, int>)getter.CreateDelegate(typeof(Func<ISymbol, int>));
    }

    private static bool TryGetMetadata(
        INamedTypeSymbol type,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out MetadataReader? reader,
        out TypeDefinitionHandle handle)
    {
        if (type.DeclaringSyntaxReferences.IsEmpty && type.ContainingModule.GetMetadata() is { } metadata)
        {
            reader = metadata.GetMetadataReader();
            int token = s_metadataTokenAccessor?.Invoke(type) ?? 0;
            if (token != 0)
            {
                handle = (TypeDefinitionHandle)MetadataTokens.EntityHandle(token);
            }
            else
            {
                string metadataName = GetMetadataName(type);
                handle = default;
                foreach (TypeDefinitionHandle candidate in reader.TypeDefinitions)
                {
                    if (AttributeTypeProvider.GetMetadataName(reader, candidate) == metadataName)
                    {
                        handle = candidate;
                        break;
                    }
                }

                if (handle.IsNil)
                {
                    throw new BadImageFormatException($"Cannot locate type '{metadataName}' in its declaring module.");
                }
            }

            return true;
        }

        reader = null;
        handle = default;
        return false;

        static string GetMetadataName(INamedTypeSymbol type) =>
            type.ContainingType is { } parent
                ? GetMetadataName(parent) + "+" + type.MetadataName
                : type.ContainingNamespace.IsGlobalNamespace
                    ? type.MetadataName
                    : type.ContainingNamespace.ToDisplayString() + "." + type.MetadataName;
    }

    private static IEnumerable<INamedTypeSymbol> ReadDeclaredDerivedTypes(
        INamedTypeSymbol type,
        Compilation compilation,
        MetadataReader reader,
        TypeDefinitionHandle handle)
    {
        CustomAttributeHandle attribute = MetadataHelpers.GetAttribute(
            reader, reader.GetTypeDefinition(handle).GetCustomAttributes(), AttributeNamespace, AttributeName);
        if (attribute.IsNil)
        {
            throw new BadImageFormatException($"Type '{type}' does not contain closed-type metadata.");
        }

        // Roslyn hides this attribute and recomputes a different order for PE symbols.
        CustomAttributeValue<ITypeSymbol> value = reader.GetCustomAttribute(attribute)
            .DecodeValue(new AttributeTypeProvider(compilation, type.ContainingAssembly));
        foreach (CustomAttributeNamedArgument<ITypeSymbol> argument in value.NamedArguments)
        {
            if (argument.Name != "DerivedTypes")
            {
                continue;
            }

            if (argument.Value is not ImmutableArray<CustomAttributeTypedArgument<ITypeSymbol>> types || types.IsDefault)
            {
                throw new BadImageFormatException($"Closed type '{type}' contains invalid derived-type metadata.");
            }

            foreach (CustomAttributeTypedArgument<ITypeSymbol> derivedType in types)
            {
                yield return derivedType.Value as INamedTypeSymbol
                    ?? throw new BadImageFormatException($"Closed type '{type}' contains invalid derived-type metadata.");
            }

            yield break;
        }
    }

    private static IEnumerable<INamedTypeSymbol> EnumerateNamedTypes(INamespaceOrTypeSymbol scope)
    {
        if (scope is INamedTypeSymbol type)
        {
            yield return type;
            foreach (INamedTypeSymbol nestedType in type.GetTypeMembers())
            {
                foreach (INamedTypeSymbol descendant in EnumerateNamedTypes(nestedType))
                {
                    yield return descendant;
                }
            }
        }
        else
        {
            foreach (INamespaceOrTypeSymbol member in ((INamespaceSymbol)scope).GetMembers())
            {
                foreach (INamedTypeSymbol descendant in EnumerateNamedTypes(member))
                {
                    yield return descendant;
                }
            }
        }
    }

    private static bool IsAccessibleViaInheritance(INamedTypeSymbol baseType, INamedTypeSymbol derivedType)
    {
        INamedTypeSymbol definition = baseType.OriginalDefinition;
        for (INamedTypeSymbol? current = derivedType; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, definition))
            {
                return true;
            }
        }

        return definition.TypeKind is TypeKind.Interface &&
            derivedType.AllInterfaces.Any(type => SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, definition));
    }

    private sealed class AttributeTypeProvider(Compilation compilation, IAssemblySymbol assembly) : ICustomAttributeTypeProvider<ITypeSymbol>
    {
        public ITypeSymbol GetSystemType() => ResolveType("System.Type");
        public bool IsSystemType(ITypeSymbol type) => SymbolEqualityComparer.Default.Equals(type, GetSystemType());
        public ITypeSymbol GetSZArrayType(ITypeSymbol elementType) => compilation.CreateArrayTypeSymbol(elementType);
        public ITypeSymbol GetTypeFromSerializedName(string name) => ResolveType(name.Split(',')[0].Trim());

        public ITypeSymbol GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind) =>
            ResolveType(GetMetadataName(reader, handle));

        public ITypeSymbol GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
        {
            TypeReference reference = reader.GetTypeReference(handle);
            string name = reader.GetString(reference.Name);
            string @namespace = reader.GetString(reference.Namespace);
            if (reference.ResolutionScope.Kind is HandleKind.TypeReference)
            {
                return ResolveType(GetReferenceName((TypeReferenceHandle)reference.ResolutionScope) + "+" + name);
            }

            return ResolveType(string.IsNullOrEmpty(@namespace) ? name : @namespace + "." + name);

            string GetReferenceName(TypeReferenceHandle parent)
            {
                TypeReference type = reader.GetTypeReference(parent);
                string typeName = reader.GetString(type.Name);
                return type.ResolutionScope.Kind is HandleKind.TypeReference
                    ? GetReferenceName((TypeReferenceHandle)type.ResolutionScope) + "+" + typeName
                    : reader.GetString(type.Namespace) is { Length: > 0 } ns ? ns + "." + typeName : typeName;
            }
        }

        public ITypeSymbol GetPrimitiveType(PrimitiveTypeCode typeCode) => compilation.GetSpecialType(typeCode switch
        {
            PrimitiveTypeCode.Void => SpecialType.System_Void,
            PrimitiveTypeCode.Boolean => SpecialType.System_Boolean,
            PrimitiveTypeCode.Byte => SpecialType.System_Byte,
            PrimitiveTypeCode.SByte => SpecialType.System_SByte,
            PrimitiveTypeCode.Char => SpecialType.System_Char,
            PrimitiveTypeCode.Int16 => SpecialType.System_Int16,
            PrimitiveTypeCode.UInt16 => SpecialType.System_UInt16,
            PrimitiveTypeCode.Int32 => SpecialType.System_Int32,
            PrimitiveTypeCode.UInt32 => SpecialType.System_UInt32,
            PrimitiveTypeCode.Int64 => SpecialType.System_Int64,
            PrimitiveTypeCode.UInt64 => SpecialType.System_UInt64,
            PrimitiveTypeCode.Single => SpecialType.System_Single,
            PrimitiveTypeCode.Double => SpecialType.System_Double,
            PrimitiveTypeCode.String => SpecialType.System_String,
            PrimitiveTypeCode.Object => SpecialType.System_Object,
            _ => throw new BadImageFormatException($"Unsupported custom-attribute type code '{typeCode}'."),
        });

        public PrimitiveTypeCode GetUnderlyingEnumType(ITypeSymbol type) => type is INamedTypeSymbol { EnumUnderlyingType: { } underlying }
            ? underlying.SpecialType switch
            {
                SpecialType.System_Byte => PrimitiveTypeCode.Byte,
                SpecialType.System_SByte => PrimitiveTypeCode.SByte,
                SpecialType.System_Int16 => PrimitiveTypeCode.Int16,
                SpecialType.System_UInt16 => PrimitiveTypeCode.UInt16,
                SpecialType.System_Int32 => PrimitiveTypeCode.Int32,
                SpecialType.System_UInt32 => PrimitiveTypeCode.UInt32,
                SpecialType.System_Int64 => PrimitiveTypeCode.Int64,
                SpecialType.System_UInt64 => PrimitiveTypeCode.UInt64,
                _ => throw new BadImageFormatException($"Invalid custom-attribute enum type '{type}'."),
            }
            : throw new BadImageFormatException($"Invalid custom-attribute enum type '{type}'.");

        private INamedTypeSymbol ResolveType(string name) =>
            assembly.GetTypeByMetadataName(name) ?? compilation.GetTypeByMetadataName(name)
                ?? throw new BadImageFormatException($"Cannot resolve closed-type metadata reference '{name}'.");

        public static string GetMetadataName(MetadataReader reader, TypeDefinitionHandle handle)
        {
            TypeDefinition definition = reader.GetTypeDefinition(handle);
            string name = reader.GetString(definition.Name);
            TypeDefinitionHandle parent = definition.GetDeclaringType();
            if (!parent.IsNil)
            {
                return GetMetadataName(reader, parent) + "+" + name;
            }

            string @namespace = reader.GetString(definition.Namespace);
            return string.IsNullOrEmpty(@namespace) ? name : @namespace + "." + name;
        }
    }
}
