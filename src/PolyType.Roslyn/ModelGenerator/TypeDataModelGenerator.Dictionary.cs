using Microsoft.CodeAnalysis;
using PolyType.Roslyn.Helpers;
using System.Collections.Immutable;

namespace PolyType.Roslyn;

public partial class TypeDataModelGenerator
{
    private bool TryMapDictionary(ITypeSymbol type, ref TypeDataModelGenerationContext ctx, out TypeDataModel? model, out TypeDataModelGenerationStatus status)
    {
        model = null;
        status = default;

        if (type is not INamedTypeSymbol namedType)
        {
            // Only named types can be dictionaries
            return false;
        }

        DictionaryKind kind = default;
        bool isParameterizedFactory = false;
        IMethodSymbol? factoryMethod = null;
        CollectionConstructorParameter[]? factorySignature = null;
        ITypeSymbol? keyType = null;
        ITypeSymbol? valueType = null;
        bool indexerIsExplicitImplementation = false;

        if (namedType.GetCompatibleGenericBaseType(KnownSymbols.IReadOnlyDictionaryOfTKeyTValue) is { } genericReadOnlyIDictInstance)
        {
            keyType = genericReadOnlyIDictInstance.TypeArguments[0];
            valueType = genericReadOnlyIDictInstance.TypeArguments[1];
            kind = DictionaryKind.IReadOnlyDictionaryOfKV;
        }
        else if (namedType.GetCompatibleGenericBaseType(KnownSymbols.IDictionaryOfTKeyTValue) is { } genericIDictInstance)
        {
            keyType = genericIDictInstance.TypeArguments[0];
            valueType = genericIDictInstance.TypeArguments[1];
            kind = DictionaryKind.IDictionaryOfKV;
        }
        else if (KnownSymbols.IDictionary.IsAssignableFrom(namedType))
        {
            keyType = KnownSymbols.Compilation.ObjectType;
            valueType = KnownSymbols.Compilation.ObjectType;
            kind = DictionaryKind.IDictionary;
        }
        else
        {
            return false; // Not a dictionary type
        }

        if (namedType.TypeKind is TypeKind.Interface)
        {
            if (namedType.TypeParameters.Length == 2)
            {
                if (KnownSymbols.DictionaryOfTKeyTValue?.GetCompatibleGenericBaseType(namedType.ConstructedFrom) is not null)
                {
                    // Handle IDictionary<TKey, TValue> and IReadOnlyDictionary<TKey, TValue> using Dictionary<TKey, TValue>
                    namedType = KnownSymbols.DictionaryOfTKeyTValue!.Construct(keyType, valueType);
                }
                else if (KnownSymbols.ImmutableDictionary?.GetCompatibleGenericBaseType(namedType.ConstructedFrom) is not null)
                {
                    // Handle IImmutableDictionary<TKey, TValue> using ImmutableDictionary<TKey, TValue>
                    namedType = KnownSymbols.ImmutableDictionary!.Construct(keyType, valueType);
                }
            }
            else if (SymbolEqualityComparer.Default.Equals(namedType, KnownSymbols.IDictionary))
            {
                // Handle IDictionary using Dictionary<object, object>
                namedType = KnownSymbols.DictionaryOfTKeyTValue!.Construct(keyType, valueType);
            }
        }

        var elementType = KnownSymbols.KeyValuePairOfKV!.Construct(keyType, valueType);

        if (ResolveBestCollectionCtor(
            namedType,
            namedType.GetConstructors(),
            hasAddMethod: ContainsSettableIndexer(namedType, keyType, valueType, out indexerIsExplicitImplementation),
            elementType,
            keyType,
            valueType) is { } bestCtor)
        {
            (factoryMethod, factorySignature, isParameterizedFactory) = bestCtor;
        }
        else if (GetImmutableDictionaryFactory(namedType) is { } bestImmutableDictionaryCtor)
        {
            (factoryMethod, factorySignature, isParameterizedFactory) = bestImmutableDictionaryCtor;
        }

        if ((status = IncludeNestedType(keyType, ref ctx)) != TypeDataModelGenerationStatus.Success ||
            (status = IncludeNestedType(valueType, ref ctx)) != TypeDataModelGenerationStatus.Success)
        {
            // return true but a null model to indicate that the type is an unsupported dictionary type
            return true;
        }

        model = new DictionaryDataModel
        {
            Type = type,
            Depth = TypeShapeRequirements.Full,
            KeyType = keyType,
            ValueType = valueType,
            DictionaryKind = kind,
            DerivedTypes = IncludeDerivedTypes(type, ref ctx, TypeShapeRequirements.Full),
            FactoryMethod = factoryMethod,
            FactorySignature = factorySignature?.ToImmutableArray() ?? ImmutableArray<CollectionConstructorParameter>.Empty,
            IndexerIsExplicitInterfaceImplementation = indexerIsExplicitImplementation,
        };

        return true;

        bool ContainsSettableIndexer(ITypeSymbol type, ITypeSymbol keyType, ITypeSymbol valueType, out bool isExplicitInterfaceImplementation)
        {
            bool hasSettableIndexer = type.GetAllMembers()
                .OfType<IPropertySymbol>()
                .Any(prop =>
                    prop is { IsStatic: false, IsIndexer: true, Parameters.Length: 1, SetMethod: not null } &&
                    SymbolEqualityComparer.Default.Equals(prop.Parameters[0].Type, keyType) &&
                    SymbolEqualityComparer.Default.Equals(prop.Type, valueType) &&
                    IsAccessibleSymbol(prop));

            if (!hasSettableIndexer && !type.IsValueType && kind is DictionaryKind.IDictionaryOfKV or DictionaryKind.IDictionary)
            {
                // For reference types, allow using explicit interface implementations of the indexer.
                isExplicitInterfaceImplementation = true;
                return true;
            }

            isExplicitInterfaceImplementation = false;
            return hasSettableIndexer;
        }

        (IMethodSymbol Factory, CollectionConstructorParameter[] Signature, bool IsParameterized)? GetImmutableDictionaryFactory(INamedTypeSymbol namedType)
        {
            if (SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, KnownSymbols.ImmutableDictionary))
            {
                return ResolveFactoryMethod("System.Collections.Immutable.ImmutableDictionary");
            }

            if (SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, KnownSymbols.ImmutableSortedDictionary))
            {
                return ResolveFactoryMethod("System.Collections.Immutable.ImmutableSortedDictionary");
            }

            if (SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, KnownSymbols.FrozenDictionary))
            {
                return ResolveFactoryMethod("System.Collections.Frozen.FrozenDictionary", factoryName: "ToFrozenDictionary");
            }

            if (SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, KnownSymbols.FSharpMap))
            {
                return ResolveFactoryMethod("Microsoft.FSharp.Collections.MapModule", "OfSeq");
            }

            return null;

            (IMethodSymbol Factory, CollectionConstructorParameter[] Signature, bool IsParameterized)? ResolveFactoryMethod(string typeName, string? factoryName = null)
            {
                INamedTypeSymbol? typeSymbol = KnownSymbols.Compilation.GetTypeByMetadataName(typeName);
                if (typeSymbol is null)
                {
                    return null;
                }

                var candidates = typeSymbol.GetMembers()
                    .OfType<IMethodSymbol>()
                    .Where(method =>
                        method is { IsStatic: true, TypeParameters.Length: 2 } &&
                        (factoryName is null ? method.Name is "Create" or "CreateRange" : method.Name == factoryName))
                    .Select(method => method.MakeGenericMethod(keyType, valueType)!);

                return ResolveBestCollectionCtor(type, candidates, hasAddMethod: false, elementType, keyType, valueType: null);
            }
        }
    }
}
