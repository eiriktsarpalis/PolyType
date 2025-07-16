using Microsoft.CodeAnalysis;
using PolyType.Roslyn.Helpers;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace PolyType.Roslyn;

public partial class TypeDataModelGenerator
{
    private bool TryMapEnumerable(ITypeSymbol type, ref TypeDataModelGenerationContext ctx, out TypeDataModel? model, out TypeDataModelGenerationStatus status)
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
        CollectionModelConstructionStrategy constructionStrategy = CollectionModelConstructionStrategy.None;
        ITypeSymbol? elementType;
        IMethodSymbol? addElementMethod = null;
        IMethodSymbol? factoryMethod = null;
        CollectionConstructorParameter[]? factorySignature = null;
        INamedTypeSymbol? asyncEnumerableOfT = null;
        bool addMethodIsExplicitInterfaceImplementation = false;

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
        else if ((
            type.GetCompatibleGenericBaseType(KnownSymbols.IEnumerableOfT) ??
            (asyncEnumerableOfT = type.GetCompatibleGenericBaseType(KnownSymbols.IAsyncEnumerableOfT))) is { } enumerableOfT)
        {
            kind = asyncEnumerableOfT is not null ? EnumerableKind.AsyncEnumerableOfT : EnumerableKind.IEnumerableOfT;
            elementType = enumerableOfT.TypeArguments[0];

            var ctorCandidates = ClassifyConstructors(type, elementType, elementType, valueType: null, namedType.Constructors
                .Where(ctor => ctor is { DeclaredAccessibility: Accessibility.Public, IsStatic: false }))
                .ToArray();

            if (GetImmutableCollectionFactory(namedType) is { } result)
            {
                (factoryMethod, factorySignature, constructionStrategy) = result;
            } 
            else if (
                ResolveBestConstructor(ctorCandidates.Where(ctor => ctor.Strategy is CollectionModelConstructionStrategy.Mutable)) is { } bestMutableCtor &&
                TryGetAddMethod(type, elementType, out addElementMethod, out addMethodIsExplicitInterfaceImplementation))
            {
                (factoryMethod, factorySignature, constructionStrategy) = bestMutableCtor;
            }
            else if (ResolveBestConstructor(ctorCandidates.Where(ctor => ctor.Strategy is not CollectionModelConstructionStrategy.Mutable)) is { } bestParameterizedCtor)
            {
                (factoryMethod, factorySignature, constructionStrategy) = bestParameterizedCtor;
            }
            else if (
                KnownSymbols.Compilation.TryGetCollectionBuilderAttribute(namedType, elementType, out IMethodSymbol? builderMethod, CancellationToken) &&
                ClassifyConstructor(type, elementType, elementType, valueType: null, builderMethod) is { } classifiedBuilderMethod)
            {
                (factoryMethod, factorySignature, constructionStrategy) = classifiedBuilderMethod;
            }
            else if (namedType.TypeKind == TypeKind.Interface)
            {
                INamedTypeSymbol listOfT = KnownSymbols.ListOfT!.Construct(elementType);
                if (namedType.IsAssignableFrom(listOfT) && 
                    ResolveBestMutableConstructor(namedType, declaringType: listOfT, elementType, keyType: elementType, valueType: null) is { } listCtor)
                {
                    // Handle IEnumerable<T>, ICollection<T>, IList<T>, IReadOnlyCollection<T> and IReadOnlyList<T> types using List<T>
                    (factoryMethod, factorySignature, constructionStrategy) = listCtor;
                    addElementMethod = listOfT.GetMembers("Add")
                        .OfType<IMethodSymbol>()
                        .First(m =>
                            m.Parameters.Length == 1 &&
                            SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, elementType));
                }
                else
                {
                    INamedTypeSymbol hashSetOfT = KnownSymbols.HashSetOfT!.Construct(elementType);
                    if (namedType.IsAssignableFrom(hashSetOfT) &&
                        ResolveBestMutableConstructor(namedType, declaringType: hashSetOfT, elementType, keyType: elementType, valueType: null) is { } hashCtor)
                    {
                        // Handle ISet<T> and IReadOnlySet<T> types using HashSet<T>
                        (factoryMethod, factorySignature, constructionStrategy) = hashCtor;
                        addElementMethod = hashSetOfT.GetMembers("Add")
                            .OfType<IMethodSymbol>()
                            .First(m =>
                                m.Parameters.Length == 1 &&
                                SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, elementType));
                    }
                }
            }
        }
        else if (KnownSymbols.IEnumerable.IsAssignableFrom(type))
        {
            elementType = KnownSymbols.Compilation.ObjectType;
            kind = EnumerableKind.IEnumerable;

            if (namedType.Constructors.FirstOrDefault(ctor => ctor is { DeclaredAccessibility: Accessibility.Public, Parameters: [] } && !ctor.IsStatic) is { } ctor &&
                TryGetAddMethod(type, elementType, out addElementMethod, out addMethodIsExplicitInterfaceImplementation))
            {
                constructionStrategy = CollectionModelConstructionStrategy.Mutable;
                factoryMethod = ctor;
            }
            else if (type.IsAssignableFrom(KnownSymbols.IList))
            {
                // Handle construction of IList, ICollection and IEnumerable interfaces using List<object?>
                INamedTypeSymbol listOfObject = KnownSymbols.ListOfT!.Construct(elementType);
                if (ResolveBestMutableConstructor(namedType, listOfObject, elementType, elementType, valueType: null) is { } listCtor)
                {
                    (factoryMethod, factorySignature, constructionStrategy) = listCtor;
                    addElementMethod = listOfObject.GetMembers("Add")
                        .OfType<IMethodSymbol>()
                        .FirstOrDefault(m =>
                            m is { DeclaredAccessibility: Accessibility.Public, Parameters: [{ Type: ITypeSymbol paramType }] } &&
                            SymbolEqualityComparer.Default.Equals(paramType, elementType));
                }
            }
        }
        else
        {
            // Type is not IEnumerable
            return false;
        }

        if ((status = IncludeNestedType(elementType, ref ctx)) != TypeDataModelGenerationStatus.Success)
        {
            // Return true to indicate that the type is an unsupported enumerable type
            return true;
        }

        model = new EnumerableDataModel
        {
            Type = type,
            Depth = TypeShapeRequirements.Full,
            ElementType = elementType,
            EnumerableKind = kind,
            DerivedTypes = IncludeDerivedTypes(type, ref ctx, TypeShapeRequirements.Full),
            AddElementMethod = addElementMethod,
            FactoryMethod = factoryMethod,
            FactorySignature = factorySignature?.ToImmutableArray() ?? ImmutableArray<CollectionConstructorParameter>.Empty,
            ConstructionStrategy = constructionStrategy,
            Rank = rank,
            AddMethodIsExplicitInterfaceImplementation = addMethodIsExplicitInterfaceImplementation,
        };

        return true;

        bool TryGetAddMethod(ITypeSymbol type, ITypeSymbol elementType, [NotNullWhen(true)] out IMethodSymbol? result, out bool isExplicitImplementation)
        {
            isExplicitImplementation = false;
            result = type.GetAllMembers()
                .OfType<IMethodSymbol>()
                .FirstOrDefault(method =>
                    method is { DeclaredAccessibility: Accessibility.Public, IsStatic: false, Name: "Add" or "Enqueue" or "Push", Parameters: [{ Type: ITypeSymbol parameterType }] } &&
                    SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, elementType));

            if (!type.IsValueType)
            {
                // For reference types, allow using explicit interface implementations of known Add methods.
                if (result is null && type.GetCompatibleGenericBaseType(KnownSymbols.ICollectionOfT) is { } iCollectionOfT)
                {

                    result = iCollectionOfT.GetMembers("Add")
                        .OfType<IMethodSymbol>()
                        .FirstOrDefault(m =>
                            m is { DeclaredAccessibility: Accessibility.Public, Parameters: [{ Type: ITypeSymbol paramType }] } &&
                            SymbolEqualityComparer.Default.Equals(paramType, elementType));

                    isExplicitImplementation = result is not null;
                }

                if (result is null && KnownSymbols.IList.IsAssignableFrom(type))
                {
                    result = KnownSymbols.IList.GetMembers("Add")
                        .OfType<IMethodSymbol>()
                        .FirstOrDefault(m =>
                            m is { DeclaredAccessibility: Accessibility.Public, Parameters: [{ Type: ITypeSymbol paramType }] } &&
                            SymbolEqualityComparer.Default.Equals(paramType, elementType));

                    isExplicitImplementation = result is not null;
                }
            }

            return result is not null;
        }

        (IMethodSymbol Factory, CollectionConstructorParameter[] Signature, CollectionModelConstructionStrategy Strategy)? GetImmutableCollectionFactory(INamedTypeSymbol namedType)
        {
            if (SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, KnownSymbols.ImmutableArray))
            {
                return FindCreateRangeMethods("System.Collections.Immutable.ImmutableArray");
            }

            if (SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, KnownSymbols.ImmutableList))
            {
                return FindCreateRangeMethods("System.Collections.Immutable.ImmutableList");
            }

            if (SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, KnownSymbols.ImmutableQueue))
            {
                return FindCreateRangeMethods("System.Collections.Immutable.ImmutableQueue");
            }

            if (SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, KnownSymbols.ImmutableStack))
            {
                return FindCreateRangeMethods("System.Collections.Immutable.ImmutableStack");
            }

            if (SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, KnownSymbols.ImmutableHashSet))
            {
                return FindCreateRangeMethods("System.Collections.Immutable.ImmutableHashSet");
            }

            if (SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, KnownSymbols.IImmutableSet))
            {
                return FindCreateRangeMethods("System.Collections.Immutable.ImmutableHashSet");
            }

            if (SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, KnownSymbols.ImmutableSortedSet))
            {
                return FindCreateRangeMethods("System.Collections.Immutable.ImmutableSortedSet");
            }

            if (SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, KnownSymbols.FrozenSet))
            {
                return FindCreateRangeMethods("System.Collections.Frozen.FrozenSet", "ToFrozenSet");
            }

            if (SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, KnownSymbols.FSharpList))
            {
                return FindCreateRangeMethods("Microsoft.FSharp.Collections.ListModule", factoryName: "OfSeq");
            }

            return default;

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
                        method is { IsStatic: true, TypeParameters: [_] } &&
                        factoryName is null ? method.Name is "Create" or "CreateRange" : method.Name == factoryName)
                    .Select(method => method.MakeGenericMethod(elementType)!);

                var classifiedCtors = ClassifyConstructors(type, elementType, keyType: elementType, valueType: null, candidates);
                return ResolveBestConstructor(classifiedCtors);
            }
        }
    }

    private (IMethodSymbol Method, CollectionConstructorParameter[] Signature, CollectionModelConstructionStrategy Strategy)? ResolveBestMutableConstructor(
        ITypeSymbol collectionType,
        INamedTypeSymbol declaringType,
        ITypeSymbol elementType,
        ITypeSymbol keyType,
        ITypeSymbol? valueType)
    {
        var candidates = declaringType.Constructors
            .Where(ctor => ctor is { DeclaredAccessibility: Accessibility.Public, IsStatic: false })
            .Select(ctor => ClassifyConstructor(collectionType, elementType, keyType, valueType, ctor))
            .Where(ctor => ctor is { Strategy: CollectionModelConstructionStrategy.Mutable })
            .Select(ctor => ctor!.Value);

        return ResolveBestConstructor(candidates);
    }

    private static (IMethodSymbol Method, CollectionConstructorParameter[] Signature, CollectionModelConstructionStrategy Strategy)? ResolveBestConstructor(
        IEnumerable<(IMethodSymbol Method, CollectionConstructorParameter[] Signature, CollectionModelConstructionStrategy Strategy)> candidates)
    {
        if (!candidates.Any())
        {
            return null;
        }

        // Pick the best constructor based on the following criteria:
        // 1. Prefer constructors with a comparer parameter.
        // 2. Prefer constructors with a larger arity.
        // 3. Prefer constructors with a more efficient kind (Mutable > Span > Enumerable).
        return candidates
            .OrderBy(c => (HasComparer(c.Signature) ? 0 : 1, -c.Signature.Length, RankStrategy(c.Strategy)))
            .First();

        static bool HasComparer(CollectionConstructorParameter[] signature)
        {
            return signature.Any(param => 
                param is CollectionConstructorParameter.Comparer or CollectionConstructorParameter.ComparerOptional 
                      or CollectionConstructorParameter.EqualityComparer or CollectionConstructorParameter.EqualityComparerOptional);
        }

        static int RankStrategy(CollectionModelConstructionStrategy kind) => kind switch
        {
            CollectionModelConstructionStrategy.Mutable => 0,
            CollectionModelConstructionStrategy.Span => 1,
            CollectionModelConstructionStrategy.Enumerable => 2,
            _ => int.MaxValue, // Everything else (intermediate collections) is less preferred
        };
    }

    private IEnumerable<(IMethodSymbol Method, CollectionConstructorParameter[] Signature, CollectionModelConstructionStrategy Strategy)> ClassifyConstructors(
        ITypeSymbol collectionType,
        ITypeSymbol elementType,
        ITypeSymbol keyType,
        ITypeSymbol? valueType,
        IEnumerable<IMethodSymbol> candidates)
    {
        return candidates
            .Select(candidate => ClassifyConstructor(collectionType, elementType, keyType, valueType, candidate))
            .Where(ctor => ctor is not null)
            .Select(ctor => ctor!.Value);
    }

    private (IMethodSymbol Method, CollectionConstructorParameter[] Signature, CollectionModelConstructionStrategy Strategy)? ClassifyConstructor(
        ITypeSymbol collectionType,
        ITypeSymbol elementType,
        ITypeSymbol keyType,
        ITypeSymbol? valueType,
        IMethodSymbol method)
    {
        ITypeSymbol returnType = method.MethodKind is MethodKind.Constructor
            ? method.ContainingType
            : method.ReturnType;

        if (!collectionType.IsAssignableFrom(returnType))
        {
            return null; // The method does not return a matching type.
        }
        
        if (method.Parameters.IsEmpty)
        {
            if (method.MethodKind is not MethodKind.Constructor)
            {
                return null; // Static factories need to have at least one parameter.
            }

            return (method, [], CollectionModelConstructionStrategy.Mutable);
        }

        bool foundComparer = false;
        bool foundValuesParameter = false;
        CollectionModelConstructionStrategy strategy = CollectionModelConstructionStrategy.Mutable;
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

            CollectionModelConstructionStrategy inferredParameterStrategy = parameterType switch
            {
                CollectionConstructorParameter.Span => CollectionModelConstructionStrategy.Span,
                CollectionConstructorParameter.Enumerable => CollectionModelConstructionStrategy.Enumerable,
                CollectionConstructorParameter.List => CollectionModelConstructionStrategy.List,
                CollectionConstructorParameter.HashSet => CollectionModelConstructionStrategy.HashSet,
                CollectionConstructorParameter.Dictionary => CollectionModelConstructionStrategy.Dictionary,
                _ => CollectionModelConstructionStrategy.None,
            };

            if (inferredParameterStrategy is not CollectionModelConstructionStrategy.None)
            {
                if (foundValuesParameter)
                {
                    return null; // Duplicate values parameter.
                }

                strategy = inferredParameterStrategy;
                continue;
            }

            bool isComparerParameter = parameterType is
                CollectionConstructorParameter.Comparer or
                CollectionConstructorParameter.ComparerOptional or
                CollectionConstructorParameter.EqualityComparer or
                CollectionConstructorParameter.EqualityComparerOptional;

            if (isComparerParameter)
            {
                if (foundComparer)
                {
                    return null; // duplicate comparer parameter.
                }

                foundComparer = true;
            }
        }

        if (strategy is CollectionModelConstructionStrategy.Mutable &&
            method.MethodKind is not MethodKind.Constructor)
        {
            return null; // Static methods need to accept a values parameter.
        }

        return (method, signature, strategy);

        CollectionConstructorParameter ClassifyParameter(IParameterSymbol parameter)
        {
            ITypeSymbol parameterType = parameter.Type;
            if (parameterType.IsAssignableFrom(KnownSymbols.IEnumerableOfT.Construct(elementType)))
            {
                return CollectionConstructorParameter.Enumerable;
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

            if (SymbolEqualityComparer.Default.Equals(parameterType, KnownSymbols.ReadOnlySpanOfT?.Construct(elementType)))
            {
                return CollectionConstructorParameter.Span;
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
