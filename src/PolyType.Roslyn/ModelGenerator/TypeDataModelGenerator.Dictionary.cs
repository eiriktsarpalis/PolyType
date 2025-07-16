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
        CollectionModelConstructionStrategy constructionStrategy = CollectionModelConstructionStrategy.None;
        IMethodSymbol? factoryMethod = null;
        CollectionConstructorParameter[]? factorySignature = null;
        ITypeSymbol? keyType = null;
        ITypeSymbol? valueType = null;
        bool indexerIsExplicitImplementation = false;

        if (type.GetCompatibleGenericBaseType(KnownSymbols.IReadOnlyDictionaryOfTKeyTValue) is { } genericReadOnlyIDictInstance)
        {
            keyType = genericReadOnlyIDictInstance.TypeArguments[0];
            valueType = genericReadOnlyIDictInstance.TypeArguments[1];
            kind = DictionaryKind.IReadOnlyDictionaryOfKV;
        }
        else if (type.GetCompatibleGenericBaseType(KnownSymbols.IDictionaryOfTKeyTValue) is { } genericIDictInstance)
        {
            keyType = genericIDictInstance.TypeArguments[0];
            valueType = genericIDictInstance.TypeArguments[1];
            kind = DictionaryKind.IDictionaryOfKV;
        }
        else if (KnownSymbols.IDictionary.IsAssignableFrom(type))
        {
            keyType = KnownSymbols.Compilation.ObjectType;
            valueType = KnownSymbols.Compilation.ObjectType;
            kind = DictionaryKind.IDictionary;
        }
        else
        {
            return false; // Not a dictionary type
        }

        var elementType = KnownSymbols.KeyValuePairOfKV!.Construct(keyType, valueType);
        var ctorCandidates = ClassifyConstructors(type, elementType, keyType, valueType, namedType.Constructors
            .Where(ctor => ctor is { DeclaredAccessibility: Accessibility.Public, IsStatic: false }))
            .ToArray();

        if (GetImmutableDictionaryFactory(namedType) is { } bestImmutableDictionaryCtor)
        {
            (factoryMethod, factorySignature, constructionStrategy) = bestImmutableDictionaryCtor;
        }
        else if (namedType.TypeKind == TypeKind.Interface)
        {
            if (namedType.TypeArguments.Length == 2)
            {
                if (KnownSymbols.DictionaryOfTKeyTValue?.GetCompatibleGenericBaseType(namedType.ConstructedFrom) != null)
                {
                    // Handle IDictionary<TKey, TValue> and IReadOnlyDictionary<TKey, TValue> using Dictionary<TKey, TValue>
                    INamedTypeSymbol dictOfTKeyTValue = KnownSymbols.DictionaryOfTKeyTValue!.Construct(keyType, valueType);
                    if (ResolveBestMutableConstructor(namedType, dictOfTKeyTValue, elementType, keyType, valueType) is { } dictionaryCtor)
                    {
                        (factoryMethod, factorySignature, constructionStrategy) = dictionaryCtor;
                    }
                }
                else if (KnownSymbols.ImmutableDictionary?.GetCompatibleGenericBaseType(namedType.ConstructedFrom) != null)
                {
                    // Handle IImmutableDictionary<TKey, TValue> using ImmutableDictionary<TKey, TValue>
                    INamedTypeSymbol immutableDictOfTKeyTValue = KnownSymbols.ImmutableDictionary!.Construct(keyType, valueType);
                    if (GetImmutableDictionaryFactory(immutableDictOfTKeyTValue) is { } immutableDictCtor)
                    {
                        (factoryMethod, factorySignature, constructionStrategy) = immutableDictCtor;
                    }
                }
            }
            else if (SymbolEqualityComparer.Default.Equals(namedType, KnownSymbols.IDictionary))
            {
                // Handle IDictionary using Dictionary<object, object>
                INamedTypeSymbol dictOfObject = KnownSymbols.DictionaryOfTKeyTValue!.Construct(keyType, valueType);
                if (ResolveBestMutableConstructor(namedType, dictOfObject, elementType, keyType, valueType) is { } dictionaryCtor)
                {
                    (factoryMethod, factorySignature, constructionStrategy) = dictionaryCtor;
                }
            }
        }
        else if (
            ResolveBestConstructor(ctorCandidates.Where(ctor => ctor.Strategy is CollectionModelConstructionStrategy.Mutable)) is { } bestMutableCtor &&
            ContainsSettableIndexer(type, keyType, valueType, out indexerIsExplicitImplementation))
        {
            (factoryMethod, factorySignature, constructionStrategy) = bestMutableCtor;
        }
        else if (ResolveBestConstructor(ctorCandidates.Where(ctor => ctor.Strategy is not CollectionModelConstructionStrategy.Mutable)) is { } bestParameterizedCtor)
        {
            (factoryMethod, factorySignature, constructionStrategy) = bestParameterizedCtor;
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
            ConstructionStrategy = constructionStrategy,
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

        (IMethodSymbol Factory, CollectionConstructorParameter[] Signature, CollectionModelConstructionStrategy Strategy)? GetImmutableDictionaryFactory(INamedTypeSymbol namedType)
        {
            if (SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, KnownSymbols.ImmutableDictionary))
            {
                return FindCreateRangeMethods("System.Collections.Immutable.ImmutableDictionary");
            }

            if (SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, KnownSymbols.ImmutableSortedDictionary))
            {
                return FindCreateRangeMethods("System.Collections.Immutable.ImmutableSortedDictionary");
            }

            if (SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, KnownSymbols.FrozenDictionary))
            {
                return FindCreateRangeMethods("System.Collections.Frozen.FrozenDictionary", factoryName: "ToFrozenDictionary");
            }

            if (SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, KnownSymbols.FSharpMap))
            {
                IMethodSymbol factory = KnownSymbols.Compilation.GetTypeByMetadataName("Microsoft.FSharp.Collections.MapModule")
                    .GetMethodSymbol(method =>
                        method is { IsStatic: true, IsGenericMethod: true, Name: "OfSeq", Parameters: [{ Type.Name: "IEnumerable" }] })
                    .MakeGenericMethod(namedType.TypeArguments[0], namedType.TypeArguments[1])!;

                return (factory, [CollectionConstructorParameter.TupleEnumerable], CollectionModelConstructionStrategy.TupleEnumerable);
            }

            return null;

            (IMethodSymbol Factory, CollectionConstructorParameter[] Signature, CollectionModelConstructionStrategy Strategy)? FindCreateRangeMethods(string typeName, string? factoryName = null)
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

                var classifiedCtors = ClassifyConstructors(type, elementType, keyType, valueType, candidates);
                return ResolveBestConstructor(classifiedCtors);
            }
        }
    }
}
