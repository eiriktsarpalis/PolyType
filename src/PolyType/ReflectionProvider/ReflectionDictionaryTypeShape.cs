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
    private readonly object _syncObject = new();
    private CollectionConstructionStrategy? _constructionStrategy;
    private (MethodBase Method, ConstructionSignature Signature)? _factory;
    private (MethodBase Method, ConstructionSignature Signature)? _factoryWithComparer;
    private MethodInfo? _addMethod;
    private bool _isDictionaryCtor;
    private bool _isFrozenDictionary;
    private bool _isFSharpMap;
    private bool _discoveryComplete;

    private Setter<TDictionary, KeyValuePair<TKey, TValue>>? _addDelegate;
    private MutableCollectionConstructor<TKey, TDictionary>? _mutableCtorDelegate;
    private EnumerableCollectionConstructor<TKey, KeyValuePair<TKey, TValue>, TDictionary>? _enumerableCtorDelegate;
    private SpanConstructor<TKey, KeyValuePair<TKey, TValue>, TDictionary>? _spanCtorDelegate;

    public sealed override TypeShapeKind Kind => TypeShapeKind.Dictionary;
    public sealed override object? Accept(TypeShapeVisitor visitor, object? state = null) => visitor.VisitDictionary(this, state);

    public CollectionComparerOptions ComparerOptions
    {
        get
        {
            if (!_discoveryComplete)
            {
                DetermineConstructionStrategy();
            }

            return _factoryWithComparer is null ? CollectionComparerOptions.None : ToComparerConstruction(_factoryWithComparer.Value.Signature);
        }
    }

    public CollectionConstructionStrategy ConstructionStrategy
    {
        get
        {
            if (_constructionStrategy is null)
            {
                DetermineConstructionStrategy();
            }

            return _constructionStrategy.Value;
        }
    }

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

        DebugExt.Assert(_addMethod != null);
        if (_addDelegate is null)
        {
            lock (_syncObject)
            {
                return _addDelegate ??= Provider.MemberAccessor.CreateDictionaryAddDelegate<TDictionary, TKey, TValue>(_addMethod);
            }
        }

        return _addDelegate;
    }

    public MutableCollectionConstructor<TKey, TDictionary> GetMutableConstructor()
    {
        if (ConstructionStrategy is not CollectionConstructionStrategy.Mutable)
        {
            Throw();
            static void Throw() => throw new InvalidOperationException("The current dictionary shape does not support mutation.");
        }

        DebugExt.Assert(_factory != null);

        return _mutableCtorDelegate ?? CreateMutableCtor();

        MutableCollectionConstructor<TKey, TDictionary> CreateMutableCtor()
        {
            var ctor = CreateConstructorDelegate(_factory.Value.Method);
            MutableCollectionConstructor<TKey, TDictionary> mutableCtorDelegate;
            switch ((_factory, _factoryWithComparer))
            {
                case ({ Signature: ConstructionSignature.None }, { Signature: ConstructionSignature.EqualityComparer }):
                    {
                        var comparerCtor = CreateConstructorDelegate<IEqualityComparer<TKey>>(_factoryWithComparer.Value.Method);
                        mutableCtorDelegate = (in CollectionConstructionOptions<TKey>? options) => options?.EqualityComparer is null ? ctor() : comparerCtor(options.Value.EqualityComparer);
                    }

                    break;
                case ({ Signature: ConstructionSignature.None }, { Signature: ConstructionSignature.Comparer }):
                    {
                        var comparerCtor = CreateConstructorDelegate<IComparer<TKey>>(_factoryWithComparer.Value.Method);
                        mutableCtorDelegate = (in CollectionConstructionOptions<TKey>? options) => options?.Comparer is null ? ctor() : comparerCtor(options.Value.Comparer);
                    }

                    break;
                case ({ Signature: ConstructionSignature.None }, null):
                    {
                        mutableCtorDelegate = (in CollectionConstructionOptions<TKey>? options) => ctor();
                    }

                    break;
                default:
                    throw CreateUnsupportedConstructorException();
            }

            return Interlocked.CompareExchange(ref _mutableCtorDelegate, mutableCtorDelegate, null) ?? mutableCtorDelegate;
        }
    }

    public EnumerableCollectionConstructor<TKey, KeyValuePair<TKey, TValue>, TDictionary> GetEnumerableConstructor()
    {
        if (ConstructionStrategy is not CollectionConstructionStrategy.Enumerable)
        {
            Throw();
            static void Throw() => throw new InvalidOperationException("The current dictionary shape does not support enumerable constructors.");
        }

        return _enumerableCtorDelegate ?? CreateEnumerableCtor();

        EnumerableCollectionConstructor<TKey, KeyValuePair<TKey, TValue>, TDictionary> CreateEnumerableCtor()
        {
            EnumerableCollectionConstructor<TKey, KeyValuePair<TKey, TValue>, TDictionary> enumerableCtorDelegate;

            if (_isFSharpMap)
            {
                DebugExt.Assert(_factory != null);
                var mapOfSeqDelegate = ((MethodInfo)_factory.Value.Method).CreateDelegate<Func<IEnumerable<Tuple<TKey, TValue>>, TDictionary>>();
                enumerableCtorDelegate = (IEnumerable<KeyValuePair<TKey, TValue>> values, in CollectionConstructionOptions<TKey>? options) => mapOfSeqDelegate(values.Select(kvp => new Tuple<TKey, TValue>(kvp.Key, kvp.Value)));
            }
            else
            {
                switch ((_factory, _factoryWithComparer))
                {
                    case ({ Signature: ConstructionSignature.Values }, null):
                        {
                            var ctor = CreateConstructorDelegate<IEnumerable<KeyValuePair<TKey, TValue>>>(_factory.Value.Method);
                            enumerableCtorDelegate = (IEnumerable<KeyValuePair<TKey, TValue>> values, in CollectionConstructionOptions<TKey>? options) => ctor(values);
                        }

                        break;
                    case ({ Signature: ConstructionSignature.Values }, { Signature: ConstructionSignature.ValuesEqualityComparer }):
                        {
                            var ctor = CreateConstructorDelegate<IEnumerable<KeyValuePair<TKey, TValue>>>(_factory.Value.Method);
                            var comparerCtor = CreateConstructorDelegate<IEnumerable<KeyValuePair<TKey, TValue>>, IEqualityComparer<TKey>>(_factoryWithComparer.Value.Method);
                            enumerableCtorDelegate = (IEnumerable<KeyValuePair<TKey, TValue>> values, in CollectionConstructionOptions<TKey>? options) => options?.EqualityComparer is null ? ctor(values) : comparerCtor(values, options.Value.EqualityComparer);
                        }

                        break;
                    case ({ Signature: ConstructionSignature.Values }, { Signature: ConstructionSignature.ValuesComparer }):
                        {
                            var ctor = CreateConstructorDelegate<IEnumerable<KeyValuePair<TKey, TValue>>>(_factory.Value.Method);
                            var comparerCtor = CreateConstructorDelegate<IEnumerable<KeyValuePair<TKey, TValue>>, IComparer<TKey>>(_factoryWithComparer.Value.Method);
                            enumerableCtorDelegate = (IEnumerable<KeyValuePair<TKey, TValue>> values, in CollectionConstructionOptions<TKey>? options) => options?.Comparer is null ? ctor(values) : comparerCtor(values, options.Value.Comparer);
                        }

                        break;
                    case ({ Signature: ConstructionSignature.Values }, { Signature: ConstructionSignature.EqualityComparerValues }):
                        {
                            var ctor = CreateConstructorDelegate<IEnumerable<KeyValuePair<TKey, TValue>>>(_factory.Value.Method);
                            var comparerCtor = CreateConstructorDelegate<IEqualityComparer<TKey>, IEnumerable<KeyValuePair<TKey, TValue>>>(_factoryWithComparer.Value.Method);
                            enumerableCtorDelegate = (IEnumerable<KeyValuePair<TKey, TValue>> values, in CollectionConstructionOptions<TKey>? options) => options?.EqualityComparer is null ? ctor(values) : comparerCtor(options.Value.EqualityComparer, values);
                        }

                        break;
                    case ({ Signature: ConstructionSignature.Values }, { Signature: ConstructionSignature.ComparerValues }):
                        {
                            var ctor = CreateConstructorDelegate<IEnumerable<KeyValuePair<TKey, TValue>>>(_factory.Value.Method);
                            var comparerCtor = CreateConstructorDelegate<IComparer<TKey>, IEnumerable<KeyValuePair<TKey, TValue>>>(_factoryWithComparer.Value.Method);
                            enumerableCtorDelegate = (IEnumerable<KeyValuePair<TKey, TValue>> values, in CollectionConstructionOptions<TKey>? options) => options?.Comparer is null ? ctor(values) : comparerCtor(options.Value.Comparer, values);
                        }

                        break;
                    case (null, { Signature: ConstructionSignature.ValuesEqualityComparer }):
                        {
                            // When only a comparer overload is available, we assume it accepts null.
                            var comparerCtor = CreateConstructorDelegate<IEnumerable<KeyValuePair<TKey, TValue>>, IEqualityComparer<TKey>?>(_factoryWithComparer.Value.Method);
                            enumerableCtorDelegate = (IEnumerable<KeyValuePair<TKey, TValue>> values, in CollectionConstructionOptions<TKey>? options) => comparerCtor(values, options?.EqualityComparer);
                        }

                        break;
                    default:
                        throw CreateUnsupportedConstructorException();
                }
            }

            return Interlocked.CompareExchange(ref _enumerableCtorDelegate, enumerableCtorDelegate, null) ?? enumerableCtorDelegate;
        }
    }

    public SpanConstructor<TKey, KeyValuePair<TKey, TValue>, TDictionary> GetSpanConstructor()
    {
        if (ConstructionStrategy is not CollectionConstructionStrategy.Span)
        {
            Throw();
            static void Throw() => throw new InvalidOperationException("The current enumerable shape does not support span constructors.");
        }

        DebugExt.Assert(_factory is not null);
        return _spanCtorDelegate ??= CreateSpanConstructor();

        SpanConstructor<TKey, KeyValuePair<TKey, TValue>, TDictionary> CreateSpanConstructor()
        {
            SpanConstructor<TKey, KeyValuePair<TKey, TValue>, TDictionary> spanCtorDelegate;
            if (_isDictionaryCtor)
            {
                var ctor = Provider.MemberAccessor.CreateFuncDelegate<Dictionary<TKey, TValue>, TDictionary>((ConstructorInfo)_factory.Value.Method);
                switch ((_factory, _factoryWithComparer))
                {
                    // TODO: add ReadOnlyDictionary tests with comparers.
                    case ({ Signature: ConstructionSignature.Values }, null):
                        spanCtorDelegate = (ReadOnlySpan<KeyValuePair<TKey, TValue>> span, in CollectionConstructionOptions<TKey>? options) => ctor(CollectionHelpers.CreateDictionary(span));
                        break;
                    case ({ Signature: ConstructionSignature.Values }, { Signature: ConstructionSignature.ValuesEqualityComparer }):
                        {
                            var comparerCtor = Provider.MemberAccessor.CreateFuncDelegate<Dictionary<TKey, TValue>, IEqualityComparer<TKey>, TDictionary>((ConstructorInfo)_factoryWithComparer.Value.Method);
                            spanCtorDelegate = (ReadOnlySpan<KeyValuePair<TKey, TValue>> span, in CollectionConstructionOptions<TKey>? options) => options?.EqualityComparer is null ? ctor(CollectionHelpers.CreateDictionary(span)) : comparerCtor(CollectionHelpers.CreateDictionary(span), options.Value.EqualityComparer);
                        }

                        break;
                    default:
                        throw CreateUnsupportedConstructorException();
                }
            }
            else
            {
                var ctor = CreateSpanConstructorDelegate<KeyValuePair<TKey, TValue>>(_factory.Value.Method);
                switch ((_factory, _factoryWithComparer))
                {
                    case ({ Signature: ConstructionSignature.Values }, null):
                        spanCtorDelegate = (ReadOnlySpan<KeyValuePair<TKey, TValue>> span, in CollectionConstructionOptions<TKey>? options) => ctor(span);
                        break;
                    case ({ Signature: ConstructionSignature.Values }, { Signature: ConstructionSignature.ValuesEqualityComparer }):
                        var comparerCtor = CreateSpanECConstructorDelegate<TKey, KeyValuePair<TKey, TValue>>(_factoryWithComparer.Value.Method);
                        spanCtorDelegate = (ReadOnlySpan<KeyValuePair<TKey, TValue>> span, in CollectionConstructionOptions<TKey>? options) => options?.EqualityComparer is null ? ctor(span) : comparerCtor(span, options.Value.EqualityComparer);
                        break;
                    default:
                        throw CreateUnsupportedConstructorException();
                }
            }

            return Interlocked.CompareExchange(ref _spanCtorDelegate, spanCtorDelegate, null) ?? spanCtorDelegate;
        }
    }

    [MemberNotNull(nameof(_constructionStrategy))]
    private void DetermineConstructionStrategy()
    {
        // There are many fields that are initialized by this method.
        // We actually initialize them sometimes multiple times along the way.
        // For thread-safety, we need to ensure that we do not recognize halfway initialized as fully initialized.
        // _discoveryComplete should be sufficient, but the compiler wants to see that we initialized the two fields
        // that we guarantee by attribute are initialized, so we check for null there too, though it's superfluous.
        if (!_discoveryComplete || _constructionStrategy is null)
        {
            lock (_syncObject)
            {
                if (!_discoveryComplete || _constructionStrategy is null)
                {
                    Helper();
                    _discoveryComplete = true;
                }
            }
        }

        [MemberNotNull(nameof(_constructionStrategy))]
        void Helper()
        {
            //if (Type == typeof(ReadOnlyDictionary<int, int>)) Debugger.Launch();
            // TODO resolve CollectionBuilderAttribute once added for Dictionary types

            Type dictionaryType = typeof(TDictionary);
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

            if (dictionaryType.GetConstructor([]) is ConstructorInfo defaultCtor)
            {
                MethodInfo? addMethod = dictionaryType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m =>
                        m.Name is "set_Item" or "Add" &&
                        m.GetParameters() is [ParameterInfo key, ParameterInfo value] &&
                        key.ParameterType == typeof(TKey) && value.ParameterType == typeof(TValue))
                    .OrderByDescending(m => m.Name) // Prefer set_Item over Add
                    .FirstOrDefault();

                if (!typeof(TDictionary).IsValueType)
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
                    _factory = (defaultCtor, ConstructionSignature.None);
                    _addMethod = addMethod;
                    SetComparerConstructionOverload(CollectionConstructionStrategy.Mutable);
                    return;
                }
            }

            if (Provider.Options.UseReflectionEmit && dictionaryType.GetConstructor([typeof(ReadOnlySpan<KeyValuePair<TKey, TValue>>)]) is ConstructorInfo spanCtor)
            {
                // Cannot invoke constructors with ROS parameters without Ref.Emit
                _factory = (spanCtor, ConstructionSignature.Values);
                SetComparerConstructionOverload(CollectionConstructionStrategy.Span);
                return;
            }

            if (dictionaryType.GetConstructor([typeof(IEnumerable<KeyValuePair<TKey, TValue>>)]) is ConstructorInfo enumerableCtor)
            {
                _factory = (enumerableCtor, ConstructionSignature.Values);
                SetComparerConstructionOverload(CollectionConstructionStrategy.Enumerable);
                return;
            }

            if (dictionaryType.GetConstructors()
                .FirstOrDefault(ctor => ctor.GetParameters() is [{ ParameterType: Type { IsGenericType: true } paramTy }] && paramTy.IsAssignableFrom(typeof(Dictionary<TKey, TValue>)))
                is ConstructorInfo dictionaryCtor)
            {
                // Handle types with ctors accepting IDictionary or IReadOnlyDictionary such as ReadOnlyDictionary<TKey, TValue>
                _factory = (dictionaryCtor, ConstructionSignature.Values);
                _isDictionaryCtor = true;
                SetComparerConstructionOverload(CollectionConstructionStrategy.Span);
                return;
            }

            if (dictionaryType is { Name: "ImmutableDictionary`2", Namespace: "System.Collections.Immutable" })
            {
                Type? factoryType = dictionaryType.Assembly.GetType("System.Collections.Immutable.ImmutableDictionary");
                MethodBase? enumerableFactory = factoryType?.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Where(m => m.Name is "CreateRange")
                    .Where(m => m.GetParameters() is [ParameterInfo p1] && p1.ParameterType.IsIEnumerable())
                    .Select(m => m.MakeGenericMethod(typeof(TKey), typeof(TValue)))
                    .FirstOrDefault();
                if (enumerableFactory is not null)
                {
                    _factory = (enumerableFactory, ConstructionSignature.Values);
                }

                SetComparerConstructionOverload(CollectionConstructionStrategy.Enumerable);
                return;
            }

            if (dictionaryType is { Name: "ImmutableSortedDictionary`2", Namespace: "System.Collections.Immutable" })
            {
                Type? factoryType = dictionaryType.Assembly.GetType("System.Collections.Immutable.ImmutableSortedDictionary");
                MethodInfo? factory = factoryType?.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Where(m => m.Name is "CreateRange")
                    .Where(m => m.GetParameters() is [ParameterInfo p1] && p1.ParameterType.IsIEnumerable())
                    .Select(m => m.MakeGenericMethod(typeof(TKey), typeof(TValue)))
                    .FirstOrDefault();

                if (factory is not null)
                {
                    _factory = (factory, ConstructionSignature.Values);
                }

                SetComparerConstructionOverload(CollectionConstructionStrategy.Enumerable);
                return;
            }

            if (dictionaryType is { Name: "FrozenDictionary`2", Namespace: "System.Collections.Frozen" })
            {
                // FrozenDictionary only has overloads that take comparers.
                Type? factoryType = dictionaryType.Assembly.GetType("System.Collections.Frozen.FrozenDictionary");
                MethodInfo? factoryWithComparer = factoryType?.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Where(m => m.Name is "ToFrozenDictionary")
                    .Where(m =>
                        m.GetParameters() is [ParameterInfo p1, ParameterInfo p2] &&
                        p1.ParameterType.IsIEnumerable() &&
                        p2.ParameterType is { IsGenericType: true } p2Type && p2Type.GetGenericTypeDefinition() == typeof(IEqualityComparer<>))
                    .Select(m => m.MakeGenericMethod(typeof(TKey), typeof(TValue)))
                    .FirstOrDefault();

                if (factoryWithComparer != null)
                {
                    _factoryWithComparer = (factoryWithComparer, ConstructionSignature.ValuesEqualityComparer);
                    _constructionStrategy = CollectionConstructionStrategy.Enumerable;
                    _isFrozenDictionary = true;
                    return;
                }
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
                    _factory = (fsharpFactory, ConstructionSignature.Values);
                    _isFSharpMap = true;
                }

                SetComparerConstructionOverload(CollectionConstructionStrategy.Enumerable);
                return;
            }

            _constructionStrategy = CollectionConstructionStrategy.None;
            return;
        }
    }

    [MemberNotNull(nameof(_constructionStrategy))]
    private void SetComparerConstructionOverload(CollectionConstructionStrategy strategy)
    {
        if (_factory is null)
        {
            _constructionStrategy = CollectionConstructionStrategy.None;
            return;
        }

        _constructionStrategy = strategy;
        _factoryWithComparer = FindComparerConstructionOverload(_factory.Value.Method, _factory.Value.Signature);
    }

    protected override CollectionConstructorParameterType ClassifyConstructorParameter(ParameterInfo parameter)
    {
        if (parameter.ParameterType is { IsGenericType: true, GenericTypeArguments: [Type typeArg] } p && p.GetGenericTypeDefinition() == typeof(IComparer<>) && typeArg == typeof(TKey))
        {
            return CollectionConstructorParameterType.IComparerOfT;
        }
        else if (parameter.ParameterType is { IsGenericType: true, GenericTypeArguments: [Type typeArg2] } p2 && p2.GetGenericTypeDefinition() == typeof(IEqualityComparer<>) && typeArg2 == typeof(TKey))
        {
            return CollectionConstructorParameterType.IEqualityComparerOfT;
        }
        else if (parameter is { ParameterType: { IsGenericType: true, GenericTypeArguments: [Type { IsGenericType: true, GenericTypeArguments: [Type k, Type v] } kvTypeArg] } }
            && kvTypeArg.GetGenericTypeDefinition() == typeof(KeyValuePair<,>)
            && k == KeyType.Type
            && v == ValueType.Type)
        {
            return CollectionConstructorParameterType.CollectionOfT;
        }

        return CollectionConstructorParameterType.Unrecognized;
    }

    private NotSupportedException CreateUnsupportedConstructorException() => new NotSupportedException($"({_factory?.Signature}, {_factoryWithComparer?.Signature}) constructor is not supported for this type.");

    private object? GetRelevantComparer(CollectionConstructionOptions<TKey>? collectionConstructionOptions)
        => GetRelevantComparer(collectionConstructionOptions, ComparerOptions);
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
