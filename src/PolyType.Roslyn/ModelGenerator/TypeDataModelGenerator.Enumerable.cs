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
        IMethodSymbol? factoryMethodWithComparer = null;
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

            // We need to establish whether comparer parameters are available first,
            // since we want to expose them if so, and that may limit other options that we offer for initializing collections.
            // For example, HashSet<T> may be constructible via CollectionBuilder syntax and thus as a Span, but specifying
            // an EqualityComparer<T> may only be available via the 'mutable' APIs.
            // Since we can only define one construction strategy, the 'mutable' strategy would win in that case.

            // Immutable collections' various factory methods.
            // Must be run before mutable collection checks since ImmutableArray
            // also has a default constructor and an Add method.
            if (GetImmutableCollectionFactory(namedType) is (not null, _, _) factories)
            {
                (factoryMethod, factoryMethodWithComparer, constructionStrategy) = factories;
            }

            // .ctor(IComparer<T>)
            if (factoryMethodWithComparer is null &&
                namedType.Constructors.FirstOrDefault(ctor => ctor is { DeclaredAccessibility: Accessibility.Public, Parameters: [{ Type: INamedTypeSymbol { IsGenericType: true } parameterType }] } &&
                (SymbolEqualityComparer.Default.Equals(parameterType.ConstructedFrom, KnownSymbols.IEqualityComparerOfT) || SymbolEqualityComparer.Default.Equals(parameterType.ConstructedFrom, KnownSymbols.IComparerOfT))) is { } ctor &&
                TryGetAddMethod(type, elementType, out addElementMethod, out addMethodIsExplicitInterfaceImplementation))
            {
                constructionStrategy = CollectionModelConstructionStrategy.Mutable;
                factoryMethodWithComparer = ctor;
            }

            // .ctor(ReadOnlySpan<T>)
            if (factoryMethod is null &&
                namedType.Constructors.FirstOrDefault(ctor => ctor is { DeclaredAccessibility: Accessibility.Public, Parameters: [{ Type: INamedTypeSymbol { IsGenericType: true } } parameter] } &&
                ClassifyConstructorParameter(parameter, elementType) == CollectionConstructorParameterType.Span) is IMethodSymbol ctor3)
            {
                constructionStrategy = CollectionModelConstructionStrategy.Span;
                factoryMethod = ctor3;

                // Look for a constructor that also takes a comparer.
                // .ctor(ReadOnlySpan<T>, I[Equality]Comparer<T>) or .ctor(I[Equality]Comparer<T>, ReadOnlySpan<T>)
                factoryMethodWithComparer = namedType.Constructors.FirstOrDefault(ctor => ctor is { DeclaredAccessibility: Accessibility.Public, Parameters: [{ } first, { } second] } && IsAcceptableConstructorPair(first, second, elementType, CollectionConstructorParameterType.Span));
            }

            // .ctor()
            if (factoryMethod is null &&
                namedType.Constructors.FirstOrDefault(ctor => ctor is { DeclaredAccessibility: Accessibility.Public, IsStatic: false, Parameters: [] }) is { } ctor2 &&
                TryGetAddMethod(type, elementType, out addElementMethod, out addMethodIsExplicitInterfaceImplementation))
            {
                constructionStrategy = CollectionModelConstructionStrategy.Mutable;
                factoryMethod = ctor2;
            }

            // .ctor(IEnumerable<T>)
            if (factoryMethod is null &&
                namedType.Constructors.FirstOrDefault(ctor =>
                ctor is { DeclaredAccessibility: Accessibility.Public, Parameters: [{ Type: INamedTypeSymbol { IsGenericType: true } } onlyParameter] } && ClassifyConstructorParameter(onlyParameter, elementType) == CollectionConstructorParameterType.List) is IMethodSymbol ctor4)
            {
                // Type exposes a constructor that accepts a subtype of List<T>
                constructionStrategy = CollectionModelConstructionStrategy.List;
                factoryMethod = ctor4;

                // Look for a constructor that also takes a comparer.
                // .ctor(IEnumerable<T>, I[Equality]Comparer<T>) or .ctor(I[Equality]Comparer<T>, IEnumerable<T>)
                factoryMethodWithComparer = namedType.Constructors.FirstOrDefault(ctor
                    => ctor is { DeclaredAccessibility: Accessibility.Public, Parameters: [{ } first, { } second] }
                    && IsAcceptableConstructorPair(first, second, elementType, CollectionConstructorParameterType.List));
            }

            // Only consider the CollectionBuilderAttribute if we don't already have a comparer factory.
            if (factoryMethodWithComparer is null &&
                KnownSymbols.Compilation.TryGetCollectionBuilderAttribute(namedType, elementType, out IMethodSymbol? builderMethod, CancellationToken))
            {
                constructionStrategy = CollectionModelConstructionStrategy.Span;
                factoryMethod = builderMethod;
            }

            if (namedType.TypeKind == TypeKind.Interface)
            {
                INamedTypeSymbol listOfT = KnownSymbols.ListOfT!.Construct(elementType);
                if (namedType.IsAssignableFrom(listOfT))
                {
                    // Handle IEnumerable<T>, ICollection<T>, IList<T>, IReadOnlyCollection<T> and IReadOnlyList<T> types using List<T>
                    constructionStrategy = CollectionModelConstructionStrategy.Mutable;
                    factoryMethod = listOfT.Constructors.First(c => c is { DeclaredAccessibility: Accessibility.Public, Parameters: [] });
                    addElementMethod = listOfT.GetMembers("Add")
                        .OfType<IMethodSymbol>()
                        .First(m =>
                            m.Parameters.Length == 1 &&
                            SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, elementType));
                }
                else
                {
                    INamedTypeSymbol hashSetOfT = KnownSymbols.HashSetOfT!.Construct(elementType);
                    if (namedType.IsAssignableFrom(hashSetOfT))
                    {
                        // Handle ISet<T> and IReadOnlySet<T> types using HashSet<T>
                        constructionStrategy = CollectionModelConstructionStrategy.Mutable;
                        factoryMethod = hashSetOfT.Constructors.First(c => c is { DeclaredAccessibility: Accessibility.Public, Parameters: [] });
                        factoryMethodWithComparer = hashSetOfT.Constructors.First(c => c is { DeclaredAccessibility: Accessibility.Public, Parameters: [{ Type.Name: "IEqualityComparer" }] });
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
                constructionStrategy = CollectionModelConstructionStrategy.Mutable;
                factoryMethod = listOfObject.Constructors.First(c => c.Parameters.IsEmpty);
                addElementMethod = listOfObject.GetMembers("Add")
                    .OfType<IMethodSymbol>()
                    .FirstOrDefault(m =>
                        m is { DeclaredAccessibility: Accessibility.Public, Parameters: [{ Type: ITypeSymbol paramType }] } &&
                        SymbolEqualityComparer.Default.Equals(paramType, elementType));
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
            ConstructionStrategy = constructionStrategy,
            AddElementMethod = addElementMethod,
            FactoryMethod = factoryMethod,
            FactoryMethodWithComparer = factoryMethodWithComparer,
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

        (IMethodSymbol? Factory, IMethodSymbol? FactoryWithComparer, CollectionModelConstructionStrategy Strategy) GetImmutableCollectionFactory(INamedTypeSymbol namedType)
        {
            IMethodSymbol? factory, factoryWithComparer;

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
                return FindCreateRangeMethods("System.Collections.Immutable.ImmutableHashSet", true);
            }

            if (SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, KnownSymbols.ImmutableSortedSet))
            {
                return FindCreateRangeMethods("System.Collections.Immutable.ImmutableSortedSet", false);
            }
            
            if (SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, KnownSymbols.FrozenSet))
            {
                factory = KnownSymbols.Compilation.GetTypeByMetadataName("System.Collections.Frozen.FrozenSet")
                    .GetMethodSymbol(method =>
                        method is { IsStatic: true, IsGenericMethod: true, Name: "ToFrozenSet", Parameters: [{ Type.Name: "IEnumerable" }, { Type.Name: "IEqualityComparer" }] })
                    .MakeGenericMethod(namedType.TypeArguments[0]);
                
                return (factory, factory, CollectionModelConstructionStrategy.List);
            }

            if (SymbolEqualityComparer.Default.Equals(namedType.ConstructedFrom, KnownSymbols.FSharpList))
            {
                factory = KnownSymbols.Compilation.GetTypeByMetadataName("Microsoft.FSharp.Collections.ListModule")
                    .GetMethodSymbol(method =>
                        method is { IsStatic: true, IsGenericMethod: true, Name: "OfSeq", Parameters: [{ Type.Name: "IEnumerable" }] })
                    .MakeGenericMethod(namedType.TypeArguments[0]);
                return (factory, null, CollectionModelConstructionStrategy.List);
            }

            return default;

            (IMethodSymbol? Factory, IMethodSymbol? FactoryWithComparer, CollectionModelConstructionStrategy Strategy) FindCreateRangeMethods(string typeName, bool? equalityComparer = null)
            {
                INamedTypeSymbol? typeSymbol = KnownSymbols.Compilation.GetTypeByMetadataName(typeName);

                // First try for the Span-based factory methods.
                CollectionModelConstructionStrategy strategy = CollectionModelConstructionStrategy.Span;
                factory = typeSymbol.GetMethodSymbol(method => method is { IsStatic: true, IsGenericMethod: true, Name: "Create", Parameters: [{ Type.Name: "ReadOnlySpan" }] })
                    .MakeGenericMethod(namedType.TypeArguments[0]);
                factoryWithComparer = equalityComparer is not null
                    ? typeSymbol.GetMethodSymbol(method => method is { IsStatic: true, IsGenericMethod: true, Name: "Create", Parameters: [{ Type.Name: string tn }, { Type.Name: "ReadOnlySpan" }] } && tn == (equalityComparer.Value ? "IEqualityComparer" : "IComparer"))
                        .MakeGenericMethod(namedType.TypeArguments[0])
                    : null;

                if (factory is null)
                {
                    strategy = CollectionModelConstructionStrategy.List;
                    factory = typeSymbol.GetMethodSymbol(method => method is { IsStatic: true, IsGenericMethod: true, Name: "CreateRange", Parameters: [{ Type.Name: "IEnumerable" }] })
                        .MakeGenericMethod(namedType.TypeArguments[0]);
                    factoryWithComparer = equalityComparer is not null
                        ? typeSymbol.GetMethodSymbol(method => method is { IsStatic: true, IsGenericMethod: true, Name: "CreateRange", Parameters: [{ Type.Name: string tn }, { Type.Name: "IEnumerable" }] } && tn == (equalityComparer.Value ? "IEqualityComparer" : "IComparer"))
                            .MakeGenericMethod(namedType.TypeArguments[0])
                        : null;
                }

                return (factory, factoryWithComparer, strategy);
            }
        }
    }

    private CollectionConstructorParameterType ClassifyConstructorParameter(IParameterSymbol parameter, ITypeSymbol elementType)
    {
        if (parameter is { Type: INamedTypeSymbol { IsGenericType: true } parameterType }
            && SymbolEqualityComparer.Default.Equals(parameterType.TypeArguments[0], elementType))
        {
            if (KnownSymbols.ListOfT?.GetCompatibleGenericBaseType(parameterType.ConstructedFrom) is not null)
            {
                return CollectionConstructorParameterType.List;
            }

            if (SymbolEqualityComparer.Default.Equals(parameterType.ConstructedFrom, KnownSymbols.ReadOnlySpanOfT))
            {
                return CollectionConstructorParameterType.Span;
            }
        }

        if (parameter is { Type: INamedTypeSymbol { IsGenericType: true, Name: "IComparer" or "IEqualityComparer", ConstructedFrom: { } typeDefinition, TypeArguments: [ITypeSymbol typeArg] } }
            && (SymbolEqualityComparer.Default.Equals(typeDefinition, KnownSymbols.IEqualityComparerOfT) || SymbolEqualityComparer.Default.Equals(typeDefinition, KnownSymbols.IComparerOfT))
            && SymbolEqualityComparer.Default.Equals(typeArg, elementType))
        {
            return parameter.Type.Name == "IComparer" ? CollectionConstructorParameterType.Comparer : CollectionConstructorParameterType.EqualityComparer;
        }

        return CollectionConstructorParameterType.None;
    }

    private bool IsAcceptableConstructorPair(IParameterSymbol first, IParameterSymbol second, ITypeSymbol elementType, CollectionConstructorParameterType collectionType)
        => IsAcceptableConstructorPair(ClassifyConstructorParameter(first, elementType), ClassifyConstructorParameter(second, elementType), collectionType);
}
