using Microsoft.CodeAnalysis;
using PolyType.Roslyn.Helpers;
using System.Collections.Immutable;

namespace PolyType.Roslyn;

public partial class TypeDataModelGenerator
{
    private bool TryMapDictionary(ITypeSymbol type, ImmutableArray<AssociatedTypeModel> associatedTypes, ref TypeDataModelGenerationContext ctx, out TypeDataModel? model, out TypeDataModelGenerationStatus status)
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
        IMethodSymbol? factoryMethodWithComparer = null;
        ITypeSymbol? keyType = null;
        ITypeSymbol? valueType = null;

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

        // .ctor()
        if (namedType.Constructors.FirstOrDefault(ctor => ctor is { Parameters: [], IsStatic: false } && IsAccessibleSymbol(ctor)) is { } ctor &&
            ContainsSettableIndexer(type, keyType, valueType))
        {
            constructionStrategy = CollectionModelConstructionStrategy.Mutable;
            factoryMethod = ctor;

            factoryMethodWithComparer = namedType.Constructors.FirstOrDefault(ctor => ctor is { Parameters: [{ Type: INamedTypeSymbol { IsGenericType: true, Name: "IEqualityComparer" } }] } && IsAccessibleSymbol(ctor));
            factoryMethodWithComparer ??= namedType.Constructors.FirstOrDefault(ctor => ctor is { Parameters: [{ Type: INamedTypeSymbol { IsGenericType: true, Name: "IComparer" } }] } && IsAccessibleSymbol(ctor));
        }

        // .ctor(ReadOnlySpan<KeyValuePair<K, V>>)
        if (factoryMethod is null && namedType.Constructors.FirstOrDefault(ctor =>
            IsAccessibleSymbol(ctor) &&
            ctor is { Parameters: [{ Type: INamedTypeSymbol { IsGenericType: true, TypeArguments: [INamedTypeSymbol { IsGenericType: true, TypeArguments: [INamedTypeSymbol k, INamedTypeSymbol v] } elementType] } parameterType }] } &&
            SymbolEqualityComparer.Default.Equals(parameterType.ConstructedFrom, KnownSymbols.ReadOnlySpanOfT) &&
            SymbolEqualityComparer.Default.Equals(elementType.ConstructedFrom, KnownSymbols.KeyValuePairOfKV) &&
            SymbolEqualityComparer.Default.Equals(k, keyType) &&
            SymbolEqualityComparer.Default.Equals(v, valueType)) is IMethodSymbol ctor2)
        {
            constructionStrategy = CollectionModelConstructionStrategy.Span;
            factoryMethod = ctor2;
        }

        if (factoryMethod is null && namedType.Constructors.FirstOrDefault(ctor =>
        {
            if (IsAccessibleSymbol(ctor) &&
                ctor.Parameters is [{ Type: INamedTypeSymbol { IsGenericType: true } parameterType }] &&
                KnownSymbols.DictionaryOfTKeyTValue?.GetCompatibleGenericBaseType(parameterType.ConstructedFrom) != null)
            {
                // Constructor accepts a single parameter that is Dictionary<,> or an interface that Dictionary<,> implements.

                if (parameterType.TypeArguments is [INamedTypeSymbol k, INamedTypeSymbol v] &&
                    SymbolEqualityComparer.Default.Equals(k, keyType) &&
                    SymbolEqualityComparer.Default.Equals(v, valueType))
                {
                    // The parameter type is Dictionary<TKey, TValue>, IDictionary<TKey, TValue> or IReadOnlyDictionary<TKey, TValue>
                    return true;
                }

                if (parameterType.TypeArguments is [INamedTypeSymbol { TypeArguments: [INamedTypeSymbol k1, INamedTypeSymbol v2] } kvp] &&
                    SymbolEqualityComparer.Default.Equals(kvp.ConstructedFrom, KnownSymbols.KeyValuePairOfKV) &&
                    SymbolEqualityComparer.Default.Equals(k1, keyType) &&
                    SymbolEqualityComparer.Default.Equals(v2, valueType))
                {
                    // The parameter type is IEnumerable<KeyValuePair<TKey, TValue>>, ICollection<KeyValuePair<TKey, TValue>> or IReadOnlyCollection<KeyValuePair<TKey, TValue>>
                    return true;
                }
            }

            return false;
        }) is IMethodSymbol ctor3)
        {
            constructionStrategy = CollectionModelConstructionStrategy.Dictionary;
            factoryMethod = ctor3;
        }

        if (factoryMethod is null && GetImmutableDictionaryFactory(namedType) is (not null, _, _) factories)
        {
            (factoryMethod, factoryMethodWithComparer, constructionStrategy) = factories;
        }

        if (namedType.TypeKind == TypeKind.Interface)
        {
            if (namedType.TypeArguments.Length == 2 && KnownSymbols.DictionaryOfTKeyTValue?.GetCompatibleGenericBaseType(namedType.ConstructedFrom) != null)
            {
                // Handle IDictionary<TKey, TValue> and IReadOnlyDictionary<TKey, TValue> using Dictionary<TKey, TValue>
                INamedTypeSymbol dictOfTKeyTValue = KnownSymbols.DictionaryOfTKeyTValue!.Construct(keyType, valueType);
                constructionStrategy = CollectionModelConstructionStrategy.Mutable;
                factoryMethod = dictOfTKeyTValue.Constructors.FirstOrDefault(ctor => ctor.Parameters.IsEmpty);
            }
            else if (SymbolEqualityComparer.Default.Equals(namedType, KnownSymbols.IDictionary))
            {
                // Handle IDictionary using Dictionary<object, object>
                INamedTypeSymbol dictOfObject = KnownSymbols.DictionaryOfTKeyTValue!.Construct(keyType, valueType);
                constructionStrategy = CollectionModelConstructionStrategy.Mutable;
                factoryMethod = dictOfObject.Constructors.FirstOrDefault(ctor => ctor.Parameters.IsEmpty);
            }
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
            FactoryMethodWithComparer = factoryMethodWithComparer,
            AssociatedTypes = associatedTypes,
        };

        return true;

        bool ContainsSettableIndexer(ITypeSymbol type, ITypeSymbol keyType, ITypeSymbol valueType)
        {
            return type.GetAllMembers()
                .OfType<IPropertySymbol>()
                .Any(prop =>
                    prop is { IsStatic: false, IsIndexer: true, Parameters.Length: 1, SetMethod: not null } &&
                    SymbolEqualityComparer.Default.Equals(prop.Parameters[0].Type, keyType) &&
                    SymbolEqualityComparer.Default.Equals(prop.Type, valueType) &&
                    IsAccessibleSymbol(prop));
        }

        (IMethodSymbol? Factory, IMethodSymbol? FactoryWithComparer, CollectionModelConstructionStrategy Strategy) GetImmutableDictionaryFactory(INamedTypeSymbol namedType)
        {
            IMethodSymbol? factory, factoryWithComparer;

            if (SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, KnownSymbols.ImmutableDictionary))
            {
                factory = KnownSymbols.Compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableDictionary")
                    .GetMethodSymbol(method =>
                        method is { IsStatic: true, IsGenericMethod: true, Name: "CreateRange", Parameters: [{ Type.Name: "IEnumerable" }] })
                    .MakeGenericMethod(namedType.TypeArguments[0], namedType.TypeArguments[1]);
                factoryWithComparer = KnownSymbols.Compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableDictionary")
                    .GetMethodSymbol(method =>
                        method is { IsStatic: true, IsGenericMethod: true, Name: "CreateRange", Parameters: [{ Type.Name: "IEqualityComparer" }, { Type.Name: "IEnumerable" }] })
                    .MakeGenericMethod(namedType.TypeArguments[0], namedType.TypeArguments[1]);
                return (factory, factoryWithComparer, CollectionModelConstructionStrategy.List);
            }

            if (SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, KnownSymbols.ImmutableSortedDictionary))
            {
                factory = KnownSymbols.Compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableSortedDictionary")
                    .GetMethodSymbol(method =>
                        method is { IsStatic: true, IsGenericMethod: true, Name: "CreateRange", Parameters: [{ Type.Name: "IEnumerable" }] })
                    .MakeGenericMethod(namedType.TypeArguments[0], namedType.TypeArguments[1]);
                factoryWithComparer = KnownSymbols.Compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableSortedDictionary")
                    .GetMethodSymbol(method =>
                        method is { IsStatic: true, IsGenericMethod: true, Name: "CreateRange", Parameters: [{ Type.Name: "IComparer" }, { Type.Name: "IEnumerable" }] })
                    .MakeGenericMethod(namedType.TypeArguments[0], namedType.TypeArguments[1]);
                return (factory, factoryWithComparer, CollectionModelConstructionStrategy.List);
            }

            if (SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, KnownSymbols.FSharpMap))
            {
                factory = KnownSymbols.Compilation.GetTypeByMetadataName("Microsoft.FSharp.Collections.MapModule")
                    .GetMethodSymbol(method =>
                        method is { IsStatic: true, IsGenericMethod: true, Name: "OfSeq", Parameters: [{ Type.Name: "IEnumerable" }] })
                    .MakeGenericMethod(namedType.TypeArguments[0], namedType.TypeArguments[1]);
                factoryWithComparer = null;
                return (factory, factoryWithComparer, CollectionModelConstructionStrategy.TupleEnumerable);
            }

            return default;
        }
    }
}
