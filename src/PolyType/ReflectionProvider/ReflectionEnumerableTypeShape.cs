using PolyType.Abstractions;
using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;

namespace PolyType.ReflectionProvider;

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal abstract class ReflectionEnumerableTypeShape<TEnumerable, TElement>(ReflectionTypeShapeProvider provider) : ReflectionTypeShape<TEnumerable>(provider), IEnumerableTypeShape<TEnumerable, TElement>
{
    private CollectionConstructorInfo? _constructorInfo;
    private EnumerableAppender<TEnumerable, TElement>? _addDelegate;
    private MutableCollectionConstructor<TElement, TEnumerable>? _mutableCtorDelegate;
    private ParameterizedCollectionConstructor<TElement, TElement, TEnumerable>? _spanCtorDelegate;

    private CollectionConstructorInfo ConstructorInfo
    {
        get => _constructorInfo ?? CommonHelpers.ExchangeIfNull(ref _constructorInfo, DetermineConstructorInfo());
    }

    public virtual CollectionConstructionStrategy ConstructionStrategy => ConstructorInfo.Strategy;
    public virtual CollectionComparerOptions SupportedComparer => ConstructorInfo.ComparerOptions;

    public virtual int Rank => 1;
    public virtual bool IsAsyncEnumerable => false;
    public abstract Func<TEnumerable, IEnumerable<TElement>> GetGetEnumerable();

    public sealed override TypeShapeKind Kind => TypeShapeKind.Enumerable;
    public sealed override object? Accept(TypeShapeVisitor visitor, object? state = null) => visitor.VisitEnumerable(this, state);
    public ITypeShape<TElement> ElementType => Provider.GetShape<TElement>();
    ITypeShape IEnumerableTypeShape.ElementType => ElementType;

    public virtual EnumerableAppender<TEnumerable, TElement> GetAppender()
    {
        if (ConstructionStrategy is not CollectionConstructionStrategy.Mutable)
        {
            Throw();
            static void Throw() => throw new InvalidOperationException("The current enumerable shape does not support mutation.");
        }

        return _addDelegate ?? CommonHelpers.ExchangeIfNull(ref _addDelegate, CreateAddDelegate());

        EnumerableAppender<TEnumerable, TElement> CreateAddDelegate()
        {
            DebugExt.Assert(ConstructorInfo is MutableCollectionConstructorInfo { AddMethod: not null });
            return Provider.MemberAccessor.CreateEnumerableAppender<TEnumerable, TElement>(((MutableCollectionConstructorInfo)ConstructorInfo).AddMethod!);
        }
    }

    public virtual MutableCollectionConstructor<TElement, TEnumerable> GetMutableConstructor()
    {
        if (ConstructionStrategy is not CollectionConstructionStrategy.Mutable)
        {
            Throw();
            static void Throw() => throw new InvalidOperationException("The current enumerable shape does not support default constructors.");
        }

        return _mutableCtorDelegate ?? CommonHelpers.ExchangeIfNull(ref _mutableCtorDelegate, CreateDefaultConstructor());

        MutableCollectionConstructor<TElement, TEnumerable> CreateDefaultConstructor()
        {
            DebugExt.Assert(ConstructorInfo is MutableCollectionConstructorInfo);
            return Provider.MemberAccessor.CreateMutableCollectionConstructor<TElement, TElement, TEnumerable>((MutableCollectionConstructorInfo)ConstructorInfo);
        }
    }

    public virtual ParameterizedCollectionConstructor<TElement, TElement, TEnumerable> GetParameterizedConstructor()
    {
        if (ConstructionStrategy is not CollectionConstructionStrategy.Parameterized)
        {
            Throw();
            static void Throw() => throw new InvalidOperationException("The current enumerable shape does not support parameterized constructors.");
        }

        return _spanCtorDelegate ?? CommonHelpers.ExchangeIfNull(ref _spanCtorDelegate, CreateParameterizedConstructor());

        ParameterizedCollectionConstructor<TElement, TElement, TEnumerable> CreateParameterizedConstructor()
        {
            DebugExt.Assert(ConstructorInfo is ParameterizedCollectionConstructorInfo);
            return Provider.MemberAccessor.CreateParameterizedCollectionConstructor<TElement, TElement, TEnumerable>((ParameterizedCollectionConstructorInfo)ConstructorInfo);
        }
    }

    private CollectionConstructorInfo DetermineConstructorInfo()
    {
        if (TryGetImmutableCollectionFactory() is { } info)
        {
            return info;
        }

        Type enumerableType = typeof(TEnumerable);
        ConstructorInfo[] allCtors = DetermineImplementationType(enumerableType).GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        MethodInfo? addMethod = ResolveAddMethod(typeof(TEnumerable));
        if (Provider.ResolveBestCollectionCtor<TElement, TElement>(enumerableType, allCtors, addMethod) is { } collectionCtorInfo)
        {
            return collectionCtorInfo;
        }

        // move this later in priority order so it doesn't skip comparer options when they exist.
        IEnumerable<MethodInfo> collectionBuilderMethods = typeof(TEnumerable).GetCollectionBuilderAttributeMethods(typeof(TElement));
        if (Provider.ResolveBestCollectionCtor<TElement, TElement>(typeof(TEnumerable), collectionBuilderMethods, addMethod: null) is { } builderCtorInfo)
        {
            return builderCtorInfo;
        }

        return NoCollectionConstructorInfo.Instance;

        CollectionConstructorInfo? TryGetImmutableCollectionFactory()
        {
            if (typeof(TEnumerable) == typeof(IEnumerable) ||
                typeof(TEnumerable) == typeof(ICollection) ||
                typeof(TEnumerable) == typeof(IEnumerable<TElement>) ||
                typeof(TEnumerable) == typeof(IReadOnlyCollection<TElement>) ||
                typeof(TEnumerable) == typeof(IReadOnlyList<TElement>))
            {
                // Handle readonly list-like collection interfaces using a static factory method.
                return ResolveFactoryMethod("PolyType.SourceGenModel.CollectionHelpers", "CreateList");
            }

#if NET
            if (typeof(TEnumerable) == typeof(IReadOnlySet<TElement>))
            {
                // Handle readonly set-like collection interfaces using a static factory method.
                return ResolveFactoryMethod("PolyType.SourceGenModel.CollectionHelpers", "CreateHashSet");
            }
#endif

            if (typeof(TEnumerable) is { Name: "ImmutableArray`1", Namespace: "System.Collections.Immutable" })
            {
                return ResolveFactoryMethod("System.Collections.Immutable.ImmutableArray");
            }

            if (typeof(TEnumerable) is { Name: "ImmutableList`1", Namespace: "System.Collections.Immutable" })
            {
                return ResolveFactoryMethod("System.Collections.Immutable.ImmutableList");
            }

            if (typeof(TEnumerable) is { Name: "ImmutableQueue`1", Namespace: "System.Collections.Immutable" })
            {
                return ResolveFactoryMethod("System.Collections.Immutable.ImmutableQueue");
            }

            if (typeof(TEnumerable) is { Name: "ImmutableStack`1", Namespace: "System.Collections.Immutable" })
            {
                return ResolveFactoryMethod("System.Collections.Immutable.ImmutableStack");
            }

            if (typeof(TEnumerable) is { Name: "ImmutableHashSet`1", Namespace: "System.Collections.Immutable" } ||
                typeof(TEnumerable) is { Name: "IImmutableSet`1", Namespace: "System.Collections.Immutable" })
            {
                return ResolveFactoryMethod("System.Collections.Immutable.ImmutableHashSet");
            }

            if (typeof(TEnumerable) is { Name: "ImmutableSortedSet`1", Namespace: "System.Collections.Immutable" })
            {
                return ResolveFactoryMethod("System.Collections.Immutable.ImmutableSortedSet");
            }

            if (typeof(TEnumerable) is { Name: "FrozenSet`1", Namespace: "System.Collections.Frozen" })
            {
                return ResolveFactoryMethod("System.Collections.Frozen.FrozenSet", factoryName: "ToFrozenSet");
            }

            if (typeof(TEnumerable) is { Name: "FSharpList`1", Namespace: "Microsoft.FSharp.Collections" })
            {
                return ResolveFactoryMethod("Microsoft.FSharp.Collections.ListModule", factoryName: "OfSeq");
            }

            return null;

            CollectionConstructorInfo ResolveFactoryMethod(string typeName, string? factoryName = null)
            {
                Type? factoryType = typeof(TEnumerable).Assembly.GetType(typeName) ?? Assembly.GetExecutingAssembly().GetType(typeName);
                if (factoryType is not null)
                {
                    var candidates = factoryType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                        .Where(m => factoryName is null ? m.Name is "Create" or "CreateRange" : m.Name == factoryName)
                        .Where(m => m.IsGenericMethod && m.GetGenericArguments().Length == 1)
                        .Select(m => m.MakeGenericMethod(typeof(TElement)));

                    if (Provider.ResolveBestCollectionCtor<TElement, TElement>(typeof(TEnumerable), candidates) is { } createRangeInfo)
                    {
                        return createRangeInfo;
                    }
                }

                return NoCollectionConstructorInfo.Instance;
            }
        }

        static Type DetermineImplementationType(Type enumerableType)
        {
            if (enumerableType.IsInterface)
            {
                if (enumerableType == typeof(ICollection<TElement>) ||
                    enumerableType == typeof(IList<TElement>) ||
                    enumerableType == typeof(IList))
                {
                    // Handle IList, ICollection<T> and IList<T> types using List<T>
                    return typeof(List<TElement>);
                }
                else if (enumerableType == typeof(ISet<TElement>))
                {
                    // Handle ISet<T> types using HashSet<T>
                    return typeof(HashSet<TElement>);
                }
            }

            return enumerableType;
        }

        static MethodInfo? ResolveAddMethod(Type enumerableType)
        {
            MethodInfo? addMethod = null;
            foreach (MethodInfo methodInfo in enumerableType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (methodInfo.Name is "Add" or "Enqueue" or "Push" &&
                    methodInfo.GetParameters() is [ParameterInfo parameter] &&
                    parameter.ParameterType == typeof(TElement))
                {
                    addMethod = methodInfo;
                    break;
                }
            }

            if (!enumerableType.IsInterface)
            {
                // If no Add method was found, check for potential explicit interface implementations.
                if (addMethod is null && typeof(ICollection<TElement>).IsAssignableFrom(enumerableType))
                {
                    addMethod = typeof(ICollection<TElement>).GetMethod(nameof(ICollection<TElement>.Add));
                }

                if (addMethod is null && typeof(IList).IsAssignableFrom(enumerableType) && typeof(TElement) == typeof(object))
                {
                    addMethod = typeof(IList).GetMethod(nameof(IList.Add));
                }
            }

            return addMethod;
        }
    }
}

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class ReflectionEnumerableTypeOfTShape<TEnumerable, TElement>(ReflectionTypeShapeProvider provider)
    : ReflectionEnumerableTypeShape<TEnumerable, TElement>(provider)
    where TEnumerable : IEnumerable<TElement>
{
    public override Func<TEnumerable, IEnumerable<TElement>> GetGetEnumerable()
        => static enumerable => enumerable;
}

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class ReflectionNonGenericEnumerableTypeShape<TEnumerable>(ReflectionTypeShapeProvider provider)
    : ReflectionEnumerableTypeShape<TEnumerable, object?>(provider)
    where TEnumerable : IEnumerable
{
    public override Func<TEnumerable, IEnumerable<object?>> GetGetEnumerable()
        => static enumerable => enumerable.Cast<object?>();
}

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class ReflectionArrayTypeShape<TElement>(ReflectionTypeShapeProvider provider)
    : ReflectionEnumerableTypeShape<TElement[], TElement>(provider)
{
    public override CollectionComparerOptions SupportedComparer => CollectionComparerOptions.None;
    public override CollectionConstructionStrategy ConstructionStrategy => CollectionConstructionStrategy.Parameterized;
    public override Func<TElement[], IEnumerable<TElement>> GetGetEnumerable() => static array => array;
    public override ParameterizedCollectionConstructor<TElement, TElement, TElement[]> GetParameterizedConstructor() => static (ReadOnlySpan<TElement> span, in CollectionConstructionOptions<TElement> options) => span.ToArray();
}

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class MultiDimensionalArrayTypeShape<TEnumerable, TElement>(ReflectionTypeShapeProvider provider, int rank)
    : ReflectionEnumerableTypeShape<TEnumerable, TElement>(provider)
    where TEnumerable : IEnumerable
{
    public override CollectionComparerOptions SupportedComparer => CollectionComparerOptions.None;
    public override CollectionConstructionStrategy ConstructionStrategy => CollectionConstructionStrategy.None;
    public override int Rank => rank;
    public override Func<TEnumerable, IEnumerable<TElement>> GetGetEnumerable()
        => static enumerable => enumerable.Cast<TElement>();
}

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class ReadOnlyMemoryTypeShape<TElement>(ReflectionTypeShapeProvider provider)
    : ReflectionEnumerableTypeShape<ReadOnlyMemory<TElement>, TElement>(provider)
{
    public override CollectionComparerOptions SupportedComparer => CollectionComparerOptions.None;
    public override CollectionConstructionStrategy ConstructionStrategy => CollectionConstructionStrategy.Parameterized;
    public override Func<ReadOnlyMemory<TElement>, IEnumerable<TElement>> GetGetEnumerable() => static memory => MemoryMarshal.ToEnumerable(memory);
    public override ParameterizedCollectionConstructor<TElement, TElement, ReadOnlyMemory<TElement>> GetParameterizedConstructor() => static (ReadOnlySpan<TElement> span, in CollectionConstructionOptions<TElement> options) => span.ToArray();
}

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class MemoryTypeShape<TElement>(ReflectionTypeShapeProvider provider)
    : ReflectionEnumerableTypeShape<Memory<TElement>, TElement>(provider)
{
    public override CollectionComparerOptions SupportedComparer => CollectionComparerOptions.None;
    public override CollectionConstructionStrategy ConstructionStrategy => CollectionConstructionStrategy.Parameterized;
    public override Func<Memory<TElement>, IEnumerable<TElement>> GetGetEnumerable() => static memory => MemoryMarshal.ToEnumerable((ReadOnlyMemory<TElement>)memory);
    public override ParameterizedCollectionConstructor<TElement, TElement, Memory<TElement>> GetParameterizedConstructor() => static (ReadOnlySpan<TElement> span, in CollectionConstructionOptions<TElement> options) => span.ToArray();
}

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class ReflectionAsyncEnumerableShape<TEnumerable, TElement>(ReflectionTypeShapeProvider provider)
    : ReflectionEnumerableTypeShape<TEnumerable, TElement>(provider)
{
    public override CollectionComparerOptions SupportedComparer => CollectionComparerOptions.None;
    public override bool IsAsyncEnumerable => true;
    public override Func<TEnumerable, IEnumerable<TElement>> GetGetEnumerable() =>
        static _ => throw new InvalidOperationException("Sync enumeration of IAsyncEnumerable instances is not supported.");
}