using Microsoft.CodeAnalysis;
using PolyType.Roslyn.Helpers;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;

namespace PolyType.Roslyn;

public partial class TypeDataModelGenerator
{
    private bool TryMapEnumerable(
        ITypeSymbol type,
        ref TypeDataModelGenerationContext ctx,
        ImmutableArray<MethodDataModel> methodModels,
        ImmutableArray<EventDataModel> eventModels,
        out TypeDataModel? model,
        out TypeDataModelGenerationStatus status)
    {
        model = null;
        status = default;

        if (type.SpecialType is SpecialType.System_String)
        {
            // Do not treat string as IEnumerable<char>.
            return false;
        }

        int rank = 1;
        EnumerableKind kind;
        ITypeSymbol? elementType;
        bool isSetType = false;
        IMethodSymbol? appendMethod = null;
        bool isParameterizedFactory = false;
        IMethodSymbol? factoryMethod = null;
        CollectionConstructorParameter[]? factorySignature = null;
        EnumerableInsertionMode insertionMode = EnumerableInsertionMode.None;

        if (type is IArrayTypeSymbol array)
        {
            elementType = array.ElementType;

            if (array.Rank == 1)
            {
                kind = EnumerableKind.ArrayOfT;
            }
            else
            {
                kind = EnumerableKind.MultiDimensionalArrayOfT;
                rank = array.Rank;
            }
        }
        else if (type is not INamedTypeSymbol namedType)
        {
            // Type is not a named type
            return false;
        }
        else if (SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, KnownSymbols.SpanOfT))
        {
            kind = EnumerableKind.SpanOfT;
            elementType = namedType.TypeArguments[0];
        }
        else if (SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, KnownSymbols.ReadOnlySpanOfT))
        {
            kind = EnumerableKind.ReadOnlySpanOfT;
            elementType = namedType.TypeArguments[0];
        }
        else if (SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, KnownSymbols.MemoryOfT))
        {
            kind = EnumerableKind.MemoryOfT;
            elementType = namedType.TypeArguments[0];
        }
        else if (SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, KnownSymbols.ReadOnlyMemoryOfT))
        {
            kind = EnumerableKind.ReadOnlyMemoryOfT;
            elementType = namedType.TypeArguments[0];
        }
        else // IAsyncEnumerable<T>, IEnumerable<T>, IEnumerable
        {
            if (namedType.GetCompatibleGenericBaseType(KnownSymbols.IAsyncEnumerableOfT) is { } asyncEnumerableOfT)
            {
                kind = EnumerableKind.IAsyncEnumerableOfT;
                elementType = asyncEnumerableOfT.TypeArguments[0];
            }
            else if (namedType.GetCompatibleGenericBaseType(KnownSymbols.IEnumerableOfT) is { } enumerableOfT)
            {
                kind = EnumerableKind.IEnumerableOfT;
                elementType = enumerableOfT.TypeArguments[0];
            }
            else if (KnownSymbols.IEnumerable.IsAssignableFrom(namedType))
            {
                kind = EnumerableKind.IEnumerable;
                elementType = KnownSymbols.Compilation.ObjectType;
            }
            else
            {
                return false; // Type is not IEnumerable
            }

            if (GetImmutableCollectionFactory(namedType) is { } result)
            {
                (factoryMethod, factorySignature, isParameterizedFactory) = result;
            }
            else if (ResolveBestCollectionCtor(
                namedType,
                DetermineImplementationType(namedType).GetConstructors(),
                hasInserter: (appendMethod = ResolveAddMethod(type, elementType, out insertionMode)) is not null,
                elementType,
                keyType: elementType,
                valueType: null) is { } bestCtor)
            {
                (factoryMethod, factorySignature, isParameterizedFactory) = bestCtor;
            }
            // move this later in priority order so it doesn't skip comparer options when they exist.
            else if (
                ResolveBestCollectionCtor(
                    namedType,
                    candidates: KnownSymbols.Compilation.GetCollectionBuilderAttributeMethods(namedType, elementType, CancellationToken),
                    hasInserter: false,
                    elementType,
                    keyType: elementType,
                    valueType: null) is { } classifiedBuilderMethod)
            {
                (factoryMethod, factorySignature, isParameterizedFactory) = classifiedBuilderMethod;
            }

            if (type.GetCompatibleGenericBaseType(KnownSymbols.ISetOfT) is not null ||
                type.GetCompatibleGenericBaseType(KnownSymbols.IReadOnlySetOfT) is not null ||
                type.GetCompatibleGenericBaseType(KnownSymbols.IImmutableSetOfT) is not null)
            {
                isSetType = true;
            }
        }

        if ((status = IncludeNestedType(elementType, ref ctx)) != TypeDataModelGenerationStatus.Success)
        {
            // Return true to indicate that the type is an unsupported enumerable type
            return true;
        }

        model = new EnumerableDataModel
        {
            Type = type,
            Requirements = TypeShapeRequirements.Full,
            ElementType = elementType,
            EnumerableKind = kind,
            DerivedTypes = IncludeDerivedTypes(type, ref ctx, TypeShapeRequirements.Full),
            Methods = methodModels,
            Events = eventModels,
            AppendMethod = isParameterizedFactory ? null : appendMethod,
            InsertionMode = isParameterizedFactory ? EnumerableInsertionMode.None : insertionMode,
            FactoryMethod = factoryMethod,
            FactorySignature = factorySignature?.ToImmutableArray() ?? ImmutableArray<CollectionConstructorParameter>.Empty,
            IsSetType = isSetType,
            Rank = rank,
        };

        return true;

        IMethodSymbol? ResolveAddMethod(ITypeSymbol type, ITypeSymbol elementType, out EnumerableInsertionMode insertionMode)
        {
            insertionMode = EnumerableInsertionMode.None;
            (IMethodSymbol? result, _) = type.ResolveVisibleMembers<IMethodSymbol>()
                .FirstOrDefault(method =>
                    method.Symbol is { DeclaredAccessibility: Accessibility.Public, IsStatic: false, Name: "Add" or "Enqueue" or "Push", Parameters: [{ Type: ITypeSymbol parameterType }] } &&
                    SymbolEqualityComparer.Default.Equals(method.Symbol.Parameters[0].Type, elementType));

            if (result is not null)
            {
                insertionMode = EnumerableInsertionMode.AddMethod;
                return result;
            }

            // Allow using explicit interface implementations of known Add methods.
            if (type.GetCompatibleGenericBaseType(KnownSymbols.ICollectionOfT) is { } iCollectionOfT)
            {
                result = iCollectionOfT.GetMethods("Add", isStatic: false)
                    .FirstOrDefault(m =>
                        m is { DeclaredAccessibility: Accessibility.Public, Parameters: [{ Type: ITypeSymbol paramType }] } &&
                        SymbolEqualityComparer.Default.Equals(paramType, elementType));

                if (result is not null)
                {
                    insertionMode = EnumerableInsertionMode.ExplicitICollectionOfT;
                    return result;
                }
            }

            if (KnownSymbols.IList.IsAssignableFrom(type))
            {
                result = KnownSymbols.IList.GetMethods("Add", isStatic: false)
                    .OfType<IMethodSymbol>()
                    .FirstOrDefault(m =>
                        m is { DeclaredAccessibility: Accessibility.Public, Parameters: [{ Type: ITypeSymbol paramType }] } &&
                        SymbolEqualityComparer.Default.Equals(paramType, elementType));

                if (result is not null)
                {
                    insertionMode = EnumerableInsertionMode.ExplicitIList;
                    return result;
                }
            }

            return appendMethod;
        }

        (IMethodSymbol Factory, CollectionConstructorParameter[] Signature, bool IsParameterized)? GetImmutableCollectionFactory(INamedTypeSymbol namedType)
        {
            if (namedType.OriginalDefinition.SpecialType 
                    is SpecialType.System_Collections_IEnumerable or SpecialType.System_Collections_Generic_IEnumerable_T
                    or SpecialType.System_Collections_Generic_IReadOnlyCollection_T or SpecialType.System_Collections_Generic_IReadOnlyList_T ||
                SymbolEqualityComparer.Default.Equals(namedType, KnownSymbols.ICollection))
            {
                // Handle readonly list-like collection interfaces using a static factory method.
                return ResolveFactoryMethod("PolyType.SourceGenModel.CollectionHelpers", "CreateList");
            }

            if (SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, KnownSymbols.IReadOnlySetOfT))
            {
                // Handle readonly set-like collection interfaces using a static factory method.
                return ResolveFactoryMethod("PolyType.SourceGenModel.CollectionHelpers", "CreateHashSet");
            }

            if (SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, KnownSymbols.ImmutableArray))
            {
                return ResolveFactoryMethod("System.Collections.Immutable.ImmutableArray");
            }

            if (SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, KnownSymbols.ImmutableList))
            {
                return ResolveFactoryMethod("System.Collections.Immutable.ImmutableList");
            }

            if (SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, KnownSymbols.ImmutableQueue))
            {
                return ResolveFactoryMethod("System.Collections.Immutable.ImmutableQueue");
            }

            if (SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, KnownSymbols.ImmutableStack))
            {
                return ResolveFactoryMethod("System.Collections.Immutable.ImmutableStack");
            }

            if (SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, KnownSymbols.ImmutableHashSet))
            {
                return ResolveFactoryMethod("System.Collections.Immutable.ImmutableHashSet");
            }

            if (SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, KnownSymbols.IImmutableSet))
            {
                return ResolveFactoryMethod("System.Collections.Immutable.ImmutableHashSet");
            }

            if (SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, KnownSymbols.ImmutableSortedSet))
            {
                return ResolveFactoryMethod("System.Collections.Immutable.ImmutableSortedSet");
            }

            if (SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, KnownSymbols.FrozenSet))
            {
                return ResolveFactoryMethod("System.Collections.Frozen.FrozenSet", "ToFrozenSet");
            }

            if (SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, KnownSymbols.FSharpList))
            {
                return ResolveFactoryMethod("Microsoft.FSharp.Collections.ListModule", factoryName: "OfSeq");
            }

            return default;

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
                        method is { IsStatic: true, TypeParameters: [_] } &&
                        factoryName is null ? method.Name is "Create" or "CreateRange" : method.Name == factoryName)
                    .Select(method => method.MakeGenericMethod(elementType)!);

                return ResolveBestCollectionCtor(type, candidates, hasInserter: false, elementType, elementType, valueType: null);
            }
        }

        INamedTypeSymbol DetermineImplementationType(INamedTypeSymbol namedType)
        {
            if (namedType.TypeKind is TypeKind.Interface)
            {
                if (namedType.OriginalDefinition.SpecialType is SpecialType.System_Collections_Generic_ICollection_T 
                                                             or SpecialType.System_Collections_Generic_IList_T ||
                    SymbolEqualityComparer.Default.Equals(namedType, KnownSymbols.IList))
                {
                    // Handle IList, ICollection<T> and IList<T> types using List<T>
                    return KnownSymbols.ListOfT!.Construct(elementType);
                }
                else
                {
                    INamedTypeSymbol hashSetOfT = KnownSymbols.HashSetOfT!.Construct(elementType);
                    if (namedType.IsAssignableFrom(hashSetOfT))
                    {
                        // Handle ISet<T> types using HashSet<T>
                        return hashSetOfT;
                    }
                }
            }

            return namedType;
        }
    }

    private (IMethodSymbol Factory, CollectionConstructorParameter[] Signature, bool IsParameterized)? ResolveBestCollectionCtor(
        ITypeSymbol collectionType,
        IEnumerable<IMethodSymbol> candidates,
        bool hasInserter,
        ITypeSymbol elementType,
        ITypeSymbol keyType,
        ITypeSymbol? valueType)
    {
        var result = candidates
            .Select(method =>
            {
                var signature = ClassifyCollectionConstructor(
                    collectionType,
                    method,
                    hasInserter,
                    out CollectionConstructorParameter? valuesParam,
                    out CollectionConstructorParameter? comparerParam,
                    elementType, keyType, valueType);

                return (Factory: method, Signature: signature, ValuesParam: valuesParam, ComparerParam: comparerParam);
            })
            .Where(ctor => ctor.Signature is not null)
            // Pick the best constructor based on the following criteria:
            // 1. Prefer constructors with a comparer parameter.
            // 2. Prefer constructors with a values parameter if not using an add method.
            // 3. Prefer constructors with a larger arity.
            // 4. Prefer constructors with a more efficient kind (Mutable > Span > Enumerable).
            .OrderBy(result =>
                (RankComparer(result.ComparerParam),
                 RankStrategy(result.ValuesParam),
                 -result.Signature!.Length,
                 RankValuePerformance(result.ValuesParam)))
            .FirstOrDefault();

        if (result.Factory is null)
        {
            return null; // No suitable constructor found.
        }

        return (result.Factory, result.Signature!, IsParameterized: result.ValuesParam is not null);

        static int RankComparer(CollectionConstructorParameter? comparer)
        {
            return comparer switch
            {
                // Rank optional comparers higher than mandatory ones.
                CollectionConstructorParameter.EqualityComparerOptional or
                CollectionConstructorParameter.ComparerOptional => 0,
                CollectionConstructorParameter.EqualityComparer or
                CollectionConstructorParameter.Comparer => 1,
                _ => int.MaxValue,
            };
        }

        int RankStrategy(CollectionConstructorParameter? valueParam)
        {
            return (hasInserter, valueParam) switch
            {
                (true, null) => 0, // Mutable collections with add methods are ranked highest.
                (false, not null) => 0, // Parameterized constructors are ranked highest if an add method is not available.
                _ => int.MaxValue, // Every other combination is ranked lowest.
            };
        }

        static int RankValuePerformance(CollectionConstructorParameter? valueParam)
        {
            return valueParam switch
            {
                null => 0, // mutable collection constructors are ranked highest.
                CollectionConstructorParameter.Span => 1, // Constructors accepting span.
                CollectionConstructorParameter.List => 2, // Constructors accepting List.
                _ => int.MaxValue, // Everything is ranked equally.
            };
        }
    }

    private CollectionConstructorParameter[]? ClassifyCollectionConstructor(
        ITypeSymbol collectionType,
        IMethodSymbol method,
        bool hasAddMethod,
        out CollectionConstructorParameter? valuesParameter,
        out CollectionConstructorParameter? comparerParameter,
        ITypeSymbol elementType,
        ITypeSymbol keyType,
        ITypeSymbol? valueType)
    {
        Debug.Assert(method.MethodKind is MethodKind.Constructor || method.IsStatic);
        valuesParameter = null;
        comparerParameter = null;

        ITypeSymbol returnType = method.MethodKind is MethodKind.Constructor
            ? method.ContainingType
            : method.ReturnType;

        if (!collectionType.IsAssignableFrom(returnType))
        {
            return null; // The method does not return a matching type.
        }
        
        if (method.Parameters.IsEmpty)
        {
            if (!hasAddMethod)
            {
                return null;
            }

            return [];
        }

        var signature = new CollectionConstructorParameter[method.Parameters.Length];
        for (int i = 0; i < method.Parameters.Length; i++)
        {
            CollectionConstructorParameter parameterType = signature[i] = ClassifyParameter(method.Parameters[i]);

            if (parameterType is CollectionConstructorParameter.Unrecognized ||
                Array.IndexOf(signature, parameterType, 0, i) >= 0)
            {
                // Unrecognized or duplicated parameter type.
                return null;
            }

            bool isValuesParameter = parameterType is
                CollectionConstructorParameter.Span or
                CollectionConstructorParameter.List or
                CollectionConstructorParameter.HashSet or
                CollectionConstructorParameter.Dictionary or
                CollectionConstructorParameter.TupleEnumerable;

            if (isValuesParameter)
            {
                if (valuesParameter is not null)
                {
                    return null; // duplicate values parameter.
                }

                valuesParameter = parameterType;
                continue;
            }

            bool isComparerParameter = parameterType is
                CollectionConstructorParameter.Comparer or
                CollectionConstructorParameter.ComparerOptional or
                CollectionConstructorParameter.EqualityComparer or
                CollectionConstructorParameter.EqualityComparerOptional;

            if (isComparerParameter)
            {
                if (comparerParameter is not null)
                {
                    return null; // duplicate comparer parameter.
                }

                comparerParameter = parameterType;
            }
        }

        return signature;

        CollectionConstructorParameter ClassifyParameter(IParameterSymbol parameter)
        {
            ITypeSymbol parameterType = parameter.Type;
            INamedTypeSymbol? namedType = parameter as INamedTypeSymbol;

            if (collectionType.IsAssignableFrom(parameterType))
            {
                // The parameter is the same type as the collection, creating a recursive relationship.
                return CollectionConstructorParameter.Unrecognized;
            }

            if (SymbolEqualityComparer.Default.Equals(parameterType.OriginalDefinition, KnownSymbols.ReadOnlySpanOfT) &&
                SymbolEqualityComparer.Default.Equals(elementType, ((INamedTypeSymbol)parameterType).TypeArguments[0]))
            {
                return CollectionConstructorParameter.Span;
            }

            if (parameterType.IsAssignableFrom(KnownSymbols.ListOfT?.Construct(elementType)))
            {
                return CollectionConstructorParameter.List;
            }

            if (parameterType.IsAssignableFrom(KnownSymbols.HashSetOfT?.Construct(elementType)))
            {
                return CollectionConstructorParameter.HashSet;
            }

            if (valueType is not null && parameterType.IsAssignableFrom(KnownSymbols.DictionaryOfTKeyTValue?.Construct(keyType, valueType)))
            {
                return CollectionConstructorParameter.Dictionary;
            }

            if (valueType is not null &&
                KnownSymbols.TupleOfKV?.Construct(keyType, valueType) is { } tupleKV &&
                KnownSymbols.IEnumerableOfT.Construct(tupleKV) is { } tupleEnumerable &&
                SymbolEqualityComparer.Default.Equals(tupleEnumerable, parameterType))
            {
                return CollectionConstructorParameter.TupleEnumerable;
            }

            if (parameterType.SpecialType is SpecialType.System_Int32 && parameter.Name is "capacity" or "initialCapacity")
            {
                return parameter.IsOptional ? CollectionConstructorParameter.CapacityOptional : CollectionConstructorParameter.Capacity;
            }

            if (SymbolEqualityComparer.Default.Equals(parameterType, KnownSymbols.IEqualityComparerOfT?.Construct(keyType)))
            {
                return AcceptsNullComparer()
                    ? CollectionConstructorParameter.EqualityComparerOptional
                    : CollectionConstructorParameter.EqualityComparer;
            }

            if (SymbolEqualityComparer.Default.Equals(parameterType, KnownSymbols.IComparerOfT?.Construct(keyType)))
            {
                return AcceptsNullComparer()
                    ? CollectionConstructorParameter.ComparerOptional
                    : CollectionConstructorParameter.Comparer;
            }

            return CollectionConstructorParameter.Unrecognized;

            bool AcceptsNullComparer()
            {
                return parameter.IsOptional ||
                    parameter.NullableAnnotation is NullableAnnotation.Annotated ||
                    // Trust that all System.Collections types tolerate a nullable comparer,
                    // with the exception of ConcurrentDictionary which fails with null in framework.
                    (collectionType.ContainingNamespace.ToDisplayString().StartsWith("System.Collections", StringComparison.Ordinal) is true &&
                     (KnownSymbols.TargetFramework is not TargetFramework.Legacy || collectionType.Name is not "ConcurrentDictionary"));
            }
        }
    }
}
