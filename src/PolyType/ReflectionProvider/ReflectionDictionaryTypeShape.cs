using System.Collections;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Xml.Linq;
using PolyType.Abstractions;
using PolyType.SourceGenModel;

namespace PolyType.ReflectionProvider;

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal abstract class ReflectionDictionaryTypeShape<TDictionary, TKey, TValue>(ReflectionTypeShapeProvider provider) : ReflectionTypeShape<TDictionary>(provider), IDictionaryTypeShape<TDictionary, TKey, TValue>
    where TKey : notnull
{
    private CollectionConstructorInfo? _constructorInfo;
    private Setter<TDictionary, KeyValuePair<TKey, TValue>>? _addDelegate;
    private MutableCollectionConstructor<TKey, TDictionary>? _mutableCtorDelegate;
    private EnumerableCollectionConstructor<TKey, KeyValuePair<TKey, TValue>, TDictionary>? _enumerableCtorDelegate;
    private SpanCollectionConstructor<TKey, KeyValuePair<TKey, TValue>, TDictionary>? _spanCtorDelegate;

    private CollectionConstructorInfo ConstructorInfo
    {
        get => _constructorInfo ?? ReflectionHelpers.ExchangeIfNull(ref _constructorInfo, DetermineConstructorInfo());
    }

    public sealed override TypeShapeKind Kind => TypeShapeKind.Dictionary;
    public sealed override object? Accept(TypeShapeVisitor visitor, object? state = null) => visitor.VisitDictionary(this, state);

    public CollectionComparerOptions SupportedComparer => ConstructorInfo.ComparerOptions;
    public CollectionConstructionStrategy ConstructionStrategy => ConstructorInfo.Strategy;

    public ITypeShape<TKey> KeyType => Provider.GetShape<TKey>();
    public ITypeShape<TValue> ValueType => Provider.GetShape<TValue>();
    ITypeShape IDictionaryTypeShape.KeyType => KeyType;
    ITypeShape IDictionaryTypeShape.ValueType => ValueType;

    public abstract Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> GetGetDictionary();

    public Setter<TDictionary, KeyValuePair<TKey, TValue>> GetAddKeyValuePair()
    {
        if (ConstructionStrategy is not CollectionConstructionStrategy.Mutable)
        {
            Throw();
            static void Throw() => throw new InvalidOperationException("The current dictionary shape does not support mutation.");
        }

        return _addDelegate ?? ReflectionHelpers.ExchangeIfNull(ref _addDelegate, CreateAddKeyValuePair());

        Setter<TDictionary, KeyValuePair<TKey, TValue>> CreateAddKeyValuePair()
        {
            DebugExt.Assert(_constructorInfo is MutableCollectionConstructorInfo);
            return Provider.MemberAccessor.CreateDictionaryAddDelegate<TDictionary, TKey, TValue>(((MutableCollectionConstructorInfo)_constructorInfo).AddMethod);
        }
    }

    public MutableCollectionConstructor<TKey, TDictionary> GetMutableCollectionConstructor()
    {
        if (ConstructionStrategy is not CollectionConstructionStrategy.Mutable)
        {
            Throw();
            static void Throw() => throw new InvalidOperationException("The current dictionary shape does not support mutation.");
        }

        return _mutableCtorDelegate ?? ReflectionHelpers.ExchangeIfNull(ref _mutableCtorDelegate, CreateMutableCtor());

        MutableCollectionConstructor<TKey, TDictionary> CreateMutableCtor()
        {
            DebugExt.Assert(_constructorInfo is MutableCollectionConstructorInfo);
            return Provider.MemberAccessor.CreateMutableCollectionConstructor<TKey, TValue, TDictionary>((MutableCollectionConstructorInfo)_constructorInfo);
        }
    }

    public EnumerableCollectionConstructor<TKey, KeyValuePair<TKey, TValue>, TDictionary> GetEnumerableCollectionConstructor()
    {
        if (ConstructionStrategy is not CollectionConstructionStrategy.Enumerable)
        {
            Throw();
            static void Throw() => throw new InvalidOperationException("The current dictionary shape does not support enumerable constructors.");
        }

        return _enumerableCtorDelegate ?? ReflectionHelpers.ExchangeIfNull(ref _enumerableCtorDelegate, CreateEnumerableCtor());

        EnumerableCollectionConstructor<TKey, KeyValuePair<TKey, TValue>, TDictionary> CreateEnumerableCtor()
        {
            DebugExt.Assert(_constructorInfo is ParameterizedCollectionConstructorInfo);
            var parameterizedCtorInfo = (ParameterizedCollectionConstructorInfo)_constructorInfo;
            if (parameterizedCtorInfo.IsFSharpMap)
            {
                // F# maps are constructed using a static method that takes a sequence of class tuples.
                var mapOfSeqDelegate = ((MethodInfo)parameterizedCtorInfo.Factory).CreateDelegate<Func<IEnumerable<Tuple<TKey, TValue>>, TDictionary>>();
                return (IEnumerable<KeyValuePair<TKey, TValue>> values, in CollectionConstructionOptions<TKey> options) => mapOfSeqDelegate(values.Select(kvp => new Tuple<TKey, TValue>(kvp.Key, kvp.Value)));
            }

            return Provider.MemberAccessor.CreateEnumerableCollectionConstructor<TKey, KeyValuePair<TKey, TValue>, TDictionary>(parameterizedCtorInfo);
        }
    }

    public SpanCollectionConstructor<TKey, KeyValuePair<TKey, TValue>, TDictionary> GetSpanCollectionConstructor()
    {
        if (ConstructionStrategy is not CollectionConstructionStrategy.Span)
        {
            Throw();
            static void Throw() => throw new InvalidOperationException("The current dictionary shape does not support span constructors.");
        }

        return _spanCtorDelegate ?? ReflectionHelpers.ExchangeIfNull(ref _spanCtorDelegate, CreateSpanCtor());
        SpanCollectionConstructor<TKey, KeyValuePair<TKey, TValue>, TDictionary> CreateSpanCtor()
        {
            DebugExt.Assert(_constructorInfo is ParameterizedCollectionConstructorInfo);
            return Provider.MemberAccessor.CreateSpanCollectionConstructor<TKey, KeyValuePair<TKey, TValue>, TDictionary>((ParameterizedCollectionConstructorInfo)_constructorInfo);
        }
    }

    private CollectionConstructorInfo DetermineConstructorInfo()
    {
        // TODO resolve CollectionBuilderAttribute once added for Dictionary types

        Type dictionaryType = typeof(TDictionary);

        // The System.Collections.Generic.Dictionary<TKey, TValue> type corresponding to the current TDictionary type, if not a Dictionary<TKey, TValue>.
        // Used for resolving dictionary constructors that accept Dictionary<TKey, TValue> as a parameter.
        Type? correspondingGenericDictionaryType = !typeof(Dictionary<TKey, TValue>).IsAssignableFrom(typeof(TDictionary))
            ? dictionaryType : null;

        if (dictionaryType.IsInterface)
        {
            if (dictionaryType.IsAssignableFrom(typeof(Dictionary<TKey, TValue>)))
            {
                // Handle IDictionary, IDictionary<TKey, TValue> and IReadOnlyDictionary<TKey, TValue> using Dictionary<TKey, TValue>
                dictionaryType = typeof(Dictionary<TKey, TValue>);
            }
            else if (dictionaryType == typeof(IDictionary))
            {
                // Handle IDictionary using Dictionary<object, object>
                Debug.Assert(typeof(TKey) == typeof(object) && typeof(TValue) == typeof(object));
                dictionaryType = typeof(Dictionary<object, object>);
            }
        }

        ConstructorInfo[] allCtors = dictionaryType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        if (Provider.FindBestCollectionFactory<KeyValuePair<TKey, TValue>, TKey>(dictionaryType, allCtors, shouldBeParameterized: false)
            is (ConstructorInfo defaultCtor, CollectionConstructorParameter[] defaultCtorSignature, _))
        {
            MethodInfo? addMethod = dictionaryType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m =>
                    m.Name is "set_Item" or "Add" &&
                    m.GetParameters() is [ParameterInfo key, ParameterInfo value] &&
                    key.ParameterType == typeof(TKey) && value.ParameterType == typeof(TValue))
                .OrderByDescending(m => m.Name) // Prefer set_Item over Add
                .FirstOrDefault();

            if (!dictionaryType.IsValueType)
            {
                // If no indexer was found, check for potential explicit interface implementations.
                // Only do so if the type is not a value type, since this would force boxing otherwise.
                if (addMethod is null && typeof(IDictionary<TKey, TValue>).IsAssignableFrom(dictionaryType))
                {
                    addMethod = typeof(IDictionary<TKey, TValue>).GetMethod("set_Item", BindingFlags.Public | BindingFlags.Instance);
                }

                if (addMethod is null && typeof(IDictionary).IsAssignableFrom(dictionaryType) && typeof(TKey) == typeof(object))
                {
                    addMethod = typeof(IDictionary).GetMethod("set_Item", BindingFlags.Public | BindingFlags.Instance);
                }
            }

            if (addMethod != null)
            {
                return new MutableCollectionConstructorInfo(defaultCtor, defaultCtorSignature, addMethod);
            }
        }

        if (Provider.FindBestCollectionFactory<KeyValuePair<TKey, TValue>, TKey>(dictionaryType, allCtors, shouldBeParameterized: true, correspondingGenericDictionaryType)
            is (ConstructorInfo parameterizedCtor, CollectionConstructorParameter[] parameterizedSig, var ctorStrategy))
        {
            Debug.Assert(ctorStrategy is CollectionConstructionStrategy.Enumerable or CollectionConstructionStrategy.Span);
            return new ParameterizedCollectionConstructorInfo(parameterizedCtor, ctorStrategy, parameterizedSig);
        }

        if (dictionaryType is { Name: "ImmutableDictionary`2", Namespace: "System.Collections.Immutable" }
                           or { Name: "IImmutableDictionary`2", Namespace: "System.Collections.Immutable" })
        {
            return FindCreateRangeMethods("System.Collections.Immutable.ImmutableDictionary");
        }

        if (dictionaryType is { Name: "ImmutableSortedDictionary`2", Namespace: "System.Collections.Immutable" })
        {
            return FindCreateRangeMethods("System.Collections.Immutable.ImmutableSortedDictionary");
        }

        if (dictionaryType is { Name: "FrozenDictionary`2", Namespace: "System.Collections.Frozen" })
        {
            return FindCreateRangeMethods("System.Collections.Frozen.FrozenDictionary", "ToFrozenDictionary");
        }

        if (dictionaryType is { Name: "FSharpMap`2", Namespace: "Microsoft.FSharp.Collections" })
        {
            Type? module = dictionaryType.Assembly.GetType("Microsoft.FSharp.Collections.MapModule");
            MethodBase? fsharpFactory = module?.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name is "OfSeq")
                .Where(m => m.GetParameters() is [ParameterInfo p] && p.ParameterType.IsIEnumerable())
                .Select(m => m.MakeGenericMethod(typeof(TKey), typeof(TValue)))
                .FirstOrDefault();

            if (fsharpFactory is not null)
            {
                return new ParameterizedCollectionConstructorInfo(fsharpFactory, CollectionConstructionStrategy.Enumerable, [CollectionConstructorParameter.Unrecognized], isFSharpMap: true);
            }
        }

        return CollectionConstructorInfo.NoConstructor;

        CollectionConstructorInfo FindCreateRangeMethods(string typeName, string? factoryName = null)
        {
            Type? factoryType = dictionaryType.Assembly.GetType(typeName);
            if (factoryType is not null)
            {
                var candidates = factoryType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Where(m => factoryName is null ? m.Name is "Create" or "CreateRange" : m.Name == factoryName)
                    .Where(m => m.GetGenericArguments().Length == 2)
                    .Select(m => m.MakeGenericMethod(typeof(TKey), typeof(TValue)));

                if (Provider.FindBestCollectionFactory<KeyValuePair<TKey, TValue>, TKey>(dictionaryType, candidates, shouldBeParameterized: true)
                    is (MethodInfo factory, CollectionConstructorParameter[] signature, CollectionConstructionStrategy strategy))
                {
                    return new ParameterizedCollectionConstructorInfo(factory, strategy, signature);
                }
            }

            return CollectionConstructorInfo.NoConstructor;
        }
    }
}

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class ReflectionDictionaryOfTShape<TDictionary, TKey, TValue>(ReflectionTypeShapeProvider provider) : ReflectionDictionaryTypeShape<TDictionary, TKey, TValue>(provider)
    where TDictionary : IDictionary<TKey, TValue>
    where TKey : notnull
{
    public override Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> GetGetDictionary()
    {
        return static dict => CollectionHelpers.AsReadOnlyDictionary<TDictionary, TKey, TValue>(dict);
    }
}

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class ReflectionReadOnlyDictionaryShape<TDictionary, TKey, TValue>(ReflectionTypeShapeProvider provider) : ReflectionDictionaryTypeShape<TDictionary, TKey, TValue>(provider)
    where TDictionary : IReadOnlyDictionary<TKey, TValue>
    where TKey : notnull
{
    public override Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> GetGetDictionary()
    {
        return static dict => dict;
    }
}

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class ReflectionNonGenericDictionaryShape<TDictionary>(ReflectionTypeShapeProvider provider) : ReflectionDictionaryTypeShape<TDictionary, object, object?>(provider)
    where TDictionary : IDictionary
{
    public override Func<TDictionary, IReadOnlyDictionary<object, object?>> GetGetDictionary()
    {
        return static obj => CollectionHelpers.AsReadOnlyDictionary(obj);
    }
}
