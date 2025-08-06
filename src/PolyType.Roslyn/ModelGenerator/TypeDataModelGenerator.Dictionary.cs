using Microsoft.CodeAnalysis;
using PolyType.Roslyn.Helpers;
using System.Collections.Immutable;

namespace PolyType.Roslyn;

public partial class TypeDataModelGenerator
{
    private bool TryMapDictionary(ITypeSymbol type, ref TypeDataModelGenerationContext ctx, ImmutableArray<MethodDataModel> methodModels, out TypeDataModel? model, out TypeDataModelGenerationStatus status)
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
        DictionaryInsertionMode availableInsertionModes = DictionaryInsertionMode.None;

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

        var elementType = KnownSymbols.KeyValuePairOfKV!.Construct(keyType, valueType);
        if (GetImmutableDictionaryFactory(namedType) is { } bestImmutableDictionaryCtor)
        {
            (factoryMethod, factorySignature, isParameterizedFactory) = bestImmutableDictionaryCtor;
        }
        else if (ResolveBestCollectionCtor(
            namedType,
            DetermineImplementationType(namedType).GetConstructors(),
            hasInserter: ContainsInserter(type, keyType, valueType, out availableInsertionModes),
            elementType,
            keyType,
            valueType) is { } bestCtor)
        {
            (factoryMethod, factorySignature, isParameterizedFactory) = bestCtor;
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
            Requirements = TypeShapeRequirements.Full,
            KeyType = keyType,
            ValueType = valueType,
            DictionaryKind = kind,
            DerivedTypes = IncludeDerivedTypes(type, ref ctx, TypeShapeRequirements.Full),
            Methods = methodModels,
            FactoryMethod = factoryMethod,
            FactorySignature = factorySignature?.ToImmutableArray() ?? ImmutableArray<CollectionConstructorParameter>.Empty,
            AvailableInsertionModes = availableInsertionModes,
        };

        return true;

        bool ContainsInserter(ITypeSymbol type, ITypeSymbol keyType, ITypeSymbol valueType, out DictionaryInsertionMode availableInsertionModes)
        {
            availableInsertionModes = DictionaryInsertionMode.None;
            bool foundContainsKey = false;
            var instanceMethods = type.GetAllMembers()
                .OfType<IMethodSymbol>()
                .Where(method => method.IsStatic is false && IsAccessibleSymbol(method));

            foreach (var method in instanceMethods)
            {
                if (method.Parameters is [var p1, var p2] &&
                    SymbolEqualityComparer.Default.Equals(p1.Type, keyType) &&
                    SymbolEqualityComparer.Default.Equals(p2.Type, valueType))
                {
                    if (method.Name is "Add")
                    {
                        availableInsertionModes |= DictionaryInsertionMode.Add;
                    }
                    else if (method is { Name: "TryAdd", ReturnType.SpecialType: SpecialType.System_Boolean })
                    {
                        availableInsertionModes |= DictionaryInsertionMode.TryAdd;
                    }
                    else if (method is { Name: "set_Item", MethodKind: MethodKind.PropertySet })
                    {
                        availableInsertionModes |= DictionaryInsertionMode.SetItem;
                    }

                    continue;
                }

                if (method is { Name: "ContainsKey", Parameters: [var param], ReturnType.SpecialType: SpecialType.System_Boolean } &&
                    SymbolEqualityComparer.Default.Equals(param.Type, keyType))
                {
                    foundContainsKey = true;
                }
            }

            if (foundContainsKey && (availableInsertionModes & DictionaryInsertionMode.Add) != 0)
            {
                availableInsertionModes |= DictionaryInsertionMode.ContainsKeyAdd;
            }

            if (type.GetCompatibleGenericBaseType(KnownSymbols.IDictionaryOfTKeyTValue) is not null)
            {
                availableInsertionModes |= DictionaryInsertionMode.ExplicitIDictionaryOfT;
            }
            else if (KnownSymbols.IDictionary.IsAssignableFrom(type))
            {
                availableInsertionModes |= DictionaryInsertionMode.ExplicitIDictionary;
            }

            return availableInsertionModes is not DictionaryInsertionMode.None;
        }

        (IMethodSymbol Factory, CollectionConstructorParameter[] Signature, bool IsParameterized)? GetImmutableDictionaryFactory(INamedTypeSymbol namedType)
        {
            if (SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, KnownSymbols.IReadOnlyDictionaryOfTKeyTValue))
            {
                return ResolveFactoryMethod("PolyType.SourceGenModel.CollectionHelpers", "CreateDictionary");
            }

            if (SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, KnownSymbols.ImmutableDictionary))
            {
                return ResolveFactoryMethod("System.Collections.Immutable.ImmutableDictionary");
            }

            if (SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, KnownSymbols.ImmutableSortedDictionary) ||
                SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, KnownSymbols.IImmutableDictionary))
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

                return ResolveBestCollectionCtor(type, candidates, hasInserter: false, elementType, keyType, valueType: null);
            }
        }

        INamedTypeSymbol DetermineImplementationType(INamedTypeSymbol namedType)
        {
            if (namedType.TypeKind is TypeKind.Interface)
            {
                if (SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, KnownSymbols.IDictionaryOfTKeyTValue) ||
                    SymbolEqualityComparer.Default.Equals(namedType, KnownSymbols.IDictionary))
                {
                    // Handle IDictionary<TKey, TValue> and IDictionary using Dictionary<TKey, TValue>
                    return KnownSymbols.DictionaryOfTKeyTValue!.Construct(keyType, valueType);
                }
            }

            return namedType;
        }
    }
}
