using PolyType.Abstractions;
using PolyType.SourceGenModel;
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
    private Setter<TEnumerable, TElement>? _addDelegate;
    private MutableCollectionConstructor<TElement, TEnumerable>? _mutableCtorDelegate;
    private EnumerableCollectionConstructor<TElement, TElement, TEnumerable>? _enumerableCtorDelegate;
    private SpanCollectionConstructor<TElement, TElement, TEnumerable>? _spanCtorDelegate;

    private CollectionConstructorInfo ConstructorInfo
    {
        get => _constructorInfo ?? ReflectionHelpers.ExchangeIfNull(ref _constructorInfo, DetermineConstructorInfo());
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

    public virtual Setter<TEnumerable, TElement> GetAddElement()
    {
        if (ConstructionStrategy is not CollectionConstructionStrategy.Mutable)
        {
            Throw();
            static void Throw() => throw new InvalidOperationException("The current enumerable shape does not support mutation.");
        }

        return _addDelegate ?? ReflectionHelpers.ExchangeIfNull(ref _addDelegate, CreateAddDelegate());

        Setter<TEnumerable, TElement> CreateAddDelegate()
        {
            DebugExt.Assert(ConstructorInfo is MutableCollectionConstructorInfo);
            return Provider.MemberAccessor.CreateEnumerableAddDelegate<TEnumerable, TElement>(((MutableCollectionConstructorInfo)ConstructorInfo).AddMethod);
        }
    }

    public virtual MutableCollectionConstructor<TElement, TEnumerable> GetMutableCollectionConstructor()
    {
        if (ConstructionStrategy is not CollectionConstructionStrategy.Mutable)
        {
            Throw();
            static void Throw() => throw new InvalidOperationException("The current enumerable shape does not support default constructors.");
        }

        return _mutableCtorDelegate ?? ReflectionHelpers.ExchangeIfNull(ref _mutableCtorDelegate, CreateDefaultConstructor());

        MutableCollectionConstructor<TElement, TEnumerable> CreateDefaultConstructor()
        {
            DebugExt.Assert(ConstructorInfo is MutableCollectionConstructorInfo);
            return Provider.MemberAccessor.CreateMutableCollectionConstructor<TElement, TElement, TEnumerable>((MutableCollectionConstructorInfo)ConstructorInfo);
        }
    }

    public virtual EnumerableCollectionConstructor<TElement, TElement, TEnumerable> GetEnumerableCollectionConstructor()
    {
        if (ConstructionStrategy is not CollectionConstructionStrategy.Enumerable)
        {
            Throw();
            static void Throw() => throw new InvalidOperationException("The current enumerable shape does not support enumerable constructors.");
        }

        return _enumerableCtorDelegate ?? ReflectionHelpers.ExchangeIfNull(ref _enumerableCtorDelegate, CreateEnumerableConstructor());

        EnumerableCollectionConstructor<TElement, TElement, TEnumerable> CreateEnumerableConstructor()
        {
            DebugExt.Assert(ConstructorInfo is ParameterizedCollectionConstructorInfo);
            return Provider.MemberAccessor.CreateEnumerableCollectionConstructor<TElement, TElement, TEnumerable>((ParameterizedCollectionConstructorInfo)ConstructorInfo);
        }
    }

    public virtual SpanCollectionConstructor<TElement, TElement, TEnumerable> GetSpanCollectionConstructor()
    {
        if (ConstructionStrategy is not CollectionConstructionStrategy.Span)
        {
            Throw();
            static void Throw() => throw new InvalidOperationException("The current enumerable shape does not support span constructors.");
        }

        return _spanCtorDelegate ?? ReflectionHelpers.ExchangeIfNull(ref _spanCtorDelegate, CreateSpanConstructor());

        SpanCollectionConstructor<TElement, TElement, TEnumerable> CreateSpanConstructor()
        {
            DebugExt.Assert(ConstructorInfo is ParameterizedCollectionConstructorInfo);
            return Provider.MemberAccessor.CreateSpanCollectionConstructor<TElement, TElement, TEnumerable>((ParameterizedCollectionConstructorInfo)ConstructorInfo);
        }
    }

    private CollectionConstructorInfo DetermineConstructorInfo()
    {
        if (TryGetImmutableCollectionFactory() is { } info)
        {
            return info;
        }

        Type enumerableType = typeof(TEnumerable);
        if (enumerableType.IsInterface)
        {
            if (typeof(TEnumerable).IsAssignableFrom(typeof(List<TElement>)))
            {
                // Handle IEnumerable<T>, ICollection<T>, IList<T>, IReadOnlyCollection<T> and IReadOnlyList<T> types using List<T>
                enumerableType = typeof(List<TElement>);
            }
            else if (typeof(TEnumerable).IsAssignableFrom(typeof(HashSet<TElement>)))
            {
                // Handle ISet<T> and IReadOnlySet<T> types using HashSet<T>
                enumerableType = typeof(HashSet<TElement>);
            }
        }

        ConstructorInfo[] allCtors = enumerableType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        if (Provider.FindBestCollectionFactory<TElement, TElement>(enumerableType, allCtors, shouldBeParameterized: false)
            is (ConstructorInfo defaultCtor, CollectionConstructorParameter[] defaultCtorSignature, _))
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

            if (!enumerableType.IsValueType)
            {
                // If no Add method was found, check for potential explicit interface implementations.
                // Only do so if the type is not a value type, since this would force boxing otherwise.
                if (addMethod is null && typeof(ICollection<TElement>).IsAssignableFrom(enumerableType))
                {
                    addMethod = typeof(ICollection<TElement>).GetMethod(nameof(ICollection<TElement>.Add));
                }

                if (addMethod is null && typeof(IList).IsAssignableFrom(enumerableType) && typeof(TElement) == typeof(object))
                {
                    addMethod = typeof(IList).GetMethod(nameof(IList.Add));
                }
            }

            if (addMethod is not null)
            {
                return new MutableCollectionConstructorInfo(defaultCtor, defaultCtorSignature, addMethod);
            }
        }

        if (Provider.FindBestCollectionFactory<TElement, TElement>(enumerableType, allCtors, shouldBeParameterized: true)
            is (ConstructorInfo parameterizedCtor, CollectionConstructorParameter[] parameterizedSig, CollectionConstructionStrategy ctorStrategy))
        {
            Debug.Assert(ctorStrategy is CollectionConstructionStrategy.Enumerable or CollectionConstructionStrategy.Span);
            return new ParameterizedCollectionConstructorInfo(parameterizedCtor, ctorStrategy, parameterizedSig);
        }

        if (enumerableType is { Name: "FSharpList`1", Namespace: "Microsoft.FSharp.Collections" })
        {
            Type? module = enumerableType.Assembly.GetType("Microsoft.FSharp.Collections.ListModule");
            MethodInfo? factory = module?.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name is "OfSeq")
                .Select(m => m.MakeGenericMethod(typeof(TElement)))
                .FirstOrDefault();

            if (factory is not null)
            {
                var signature = Provider.ClassifyMethodParameters<TElement, TElement>(enumerableType, factory, shouldBeParameterized: true, out var strategy);
                DebugExt.Assert(signature is not null);
                return new ParameterizedCollectionConstructorInfo(factory, strategy, signature);
            }

            return CollectionConstructorInfo.NoConstructor;
        }

        // move this later in priority order so it doesn't skip comparer options when they exist.
        if (typeof(TEnumerable).TryGetCollectionBuilderAttribute(typeof(TElement), out MethodInfo? builderMethod) &&
            Provider.ClassifyMethodParameters<TElement, TElement>(enumerableType, builderMethod, shouldBeParameterized: true, out var builderStrategy) is { } builderSignature)
        {
            return new ParameterizedCollectionConstructorInfo(builderMethod, builderStrategy, builderSignature);
        }

        return CollectionConstructorInfo.NoConstructor;

        CollectionConstructorInfo? TryGetImmutableCollectionFactory()
        {
            if (typeof(TEnumerable) is { Name: "ImmutableArray`1", Namespace: "System.Collections.Immutable" })
            {
                return FindCreateRangeMethods("System.Collections.Immutable.ImmutableArray");
            }

            if (typeof(TEnumerable) is { Name: "ImmutableList`1", Namespace: "System.Collections.Immutable" })
            {
                return FindCreateRangeMethods("System.Collections.Immutable.ImmutableList");
            }

            if (typeof(TEnumerable) is { Name: "ImmutableQueue`1", Namespace: "System.Collections.Immutable" })
            {
                return FindCreateRangeMethods("System.Collections.Immutable.ImmutableQueue");
            }

            if (typeof(TEnumerable) is { Name: "ImmutableStack`1", Namespace: "System.Collections.Immutable" })
            {
                return FindCreateRangeMethods("System.Collections.Immutable.ImmutableStack");
            }

            if (typeof(TEnumerable) is { Name: "ImmutableHashSet`1", Namespace: "System.Collections.Immutable" } ||
                typeof(TEnumerable) is { Name: "IImmutableSet`1", Namespace: "System.Collections.Immutable" })
            {
                return FindCreateRangeMethods("System.Collections.Immutable.ImmutableHashSet");
            }

            if (typeof(TEnumerable) is { Name: "ImmutableSortedSet`1", Namespace: "System.Collections.Immutable" })
            {
                return FindCreateRangeMethods("System.Collections.Immutable.ImmutableSortedSet");
            }

            if (typeof(TEnumerable) is { Name: "FrozenSet`1", Namespace: "System.Collections.Frozen" })
            {
                return FindCreateRangeMethods("System.Collections.Frozen.FrozenSet", factoryName: "ToFrozenSet");
            }

            return null;

            CollectionConstructorInfo FindCreateRangeMethods(string typeName, string? factoryName = null)
            {
                Type? factoryType = typeof(TEnumerable).Assembly.GetType(typeName);
                if (factoryType is not null)
                {
                    var candidates = factoryType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                        .Where(m => factoryName is null ? m.Name is "Create" or "CreateRange" : m.Name == factoryName)
                        .Where(m => m.IsGenericMethod && m.GetGenericArguments().Length == 1)
                        .Select(m => m.MakeGenericMethod(typeof(TElement)));

                    if (Provider.FindBestCollectionFactory<TElement, TElement>(typeof(TEnumerable), candidates, shouldBeParameterized: true)
                        is (MethodInfo factory, CollectionConstructorParameter[] signature, CollectionConstructionStrategy strategy))
                    {
                        return new ParameterizedCollectionConstructorInfo(factory, strategy, signature);
                    }
                }

                return CollectionConstructorInfo.NoConstructor;
            }
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
    public override CollectionConstructionStrategy ConstructionStrategy => CollectionConstructionStrategy.Span;
    public override Func<TElement[], IEnumerable<TElement>> GetGetEnumerable() => static array => array;
    public override SpanCollectionConstructor<TElement, TElement, TElement[]> GetSpanCollectionConstructor() => static (ReadOnlySpan<TElement> span, in CollectionConstructionOptions<TElement> options) => span.ToArray();
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
    public override CollectionConstructionStrategy ConstructionStrategy => CollectionConstructionStrategy.Span;
    public override Func<ReadOnlyMemory<TElement>, IEnumerable<TElement>> GetGetEnumerable() => static memory => MemoryMarshal.ToEnumerable(memory);
    public override SpanCollectionConstructor<TElement, TElement, ReadOnlyMemory<TElement>> GetSpanCollectionConstructor() => static (ReadOnlySpan<TElement> span, in CollectionConstructionOptions<TElement> options) => span.ToArray();
}

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class MemoryTypeShape<TElement>(ReflectionTypeShapeProvider provider)
    : ReflectionEnumerableTypeShape<Memory<TElement>, TElement>(provider)
{
    public override CollectionComparerOptions SupportedComparer => CollectionComparerOptions.None;
    public override CollectionConstructionStrategy ConstructionStrategy => CollectionConstructionStrategy.Span;
    public override Func<Memory<TElement>, IEnumerable<TElement>> GetGetEnumerable() => static memory => MemoryMarshal.ToEnumerable((ReadOnlyMemory<TElement>)memory);
    public override SpanCollectionConstructor<TElement, TElement, Memory<TElement>> GetSpanCollectionConstructor() => static (ReadOnlySpan<TElement> span, in CollectionConstructionOptions<TElement> options) => span.ToArray();
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