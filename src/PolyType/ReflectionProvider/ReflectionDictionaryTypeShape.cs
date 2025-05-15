using PolyType.Abstractions;
using PolyType.SourceGenModel;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace PolyType.ReflectionProvider;

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal abstract class ReflectionDictionaryTypeShape<TDictionary, TKey, TValue>(ReflectionTypeShapeProvider provider) : ReflectionTypeShape<TDictionary>(provider), IDictionaryTypeShape<TDictionary, TKey, TValue>
    where TKey : notnull
{
    private CollectionConstructionStrategy? _constructionStrategy;
    private ConstructorInfo? _defaultCtor;
    private ConstructorInfo? _defaultCtorWithComparer;
    private MethodInfo? _addMethod;
    private MethodBase? _enumerableCtor;
    private MethodBase? _enumerableCtorWithComparer;
    private MethodBase? _spanCtor;
    private MethodBase? _spanCtorWithComparer;
    private ConstructorInfo? _dictionaryCtor;
    private ConstructorInfo? _dictionaryCtorWithComparer;
    private bool _isFSharpMap;
    private ConstructionWithComparer? _constructionStyle;

    private Setter<TDictionary, KeyValuePair<TKey, TValue>>? _addDelegate;
    private Func<TDictionary>? _defaultCtorDelegate;
    private Func<IEnumerable<KeyValuePair<TKey, TValue>>, TDictionary>? _enumerableCtorDelegate;
    private SpanConstructor<KeyValuePair<TKey, TValue>, TDictionary>? _spanCtorDelegate;

    public sealed override TypeShapeKind Kind => TypeShapeKind.Dictionary;
    public sealed override object? Accept(TypeShapeVisitor visitor, object? state = null) => visitor.VisitDictionary(this, state);

    public ComparerConstruction CustomComparerSupport
    {
        get
        {
            if (_constructionStyle is null)
            {
                DetermineConstructionStrategy();
            }

            return ToComparerConstruction(_constructionStyle.Value);
        }
    }

    public CollectionConstructionStrategy ConstructionStrategy => _constructionStrategy ??= DetermineConstructionStrategy();
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
        return _addDelegate ??= Provider.MemberAccessor.CreateDictionaryAddDelegate<TDictionary, TKey, TValue>(_addMethod);
    }

    public Func<TDictionary> GetDefaultConstructor(in CollectionConstructionOptions<TKey> collectionConstructionOptions)
    {
        if (ConstructionStrategy is not CollectionConstructionStrategy.Mutable)
        {
            Throw();
            static void Throw() => throw new InvalidOperationException("The current dictionary shape does not support mutation.");
        }

        DebugExt.Assert(_defaultCtor != null);

        // We'll use a shared delegate when no comparer applies.
        object? relevantComparer = GetRelevantComparer(collectionConstructionOptions);
        if (relevantComparer is null || _defaultCtorWithComparer is null)
        {
            return _defaultCtorDelegate ??= CreateDefaultCtor();
            Func<TDictionary> CreateDefaultCtor()
            {
                return Provider.MemberAccessor.CreateDefaultConstructor<TDictionary>(new MethodConstructorShapeInfo(typeof(TDictionary), _defaultCtor, parameters: []));
            }
        }
        else
        {
            // TODO: Use Ref.Emit when appropriate.
            object?[] args = [relevantComparer];
            return () => (TDictionary)_defaultCtorWithComparer.Invoke(args);
        }
    }

    public Func<IEnumerable<KeyValuePair<TKey, TValue>>, TDictionary> GetEnumerableConstructor(in CollectionConstructionOptions<TKey> collectionConstructionOptions)
    {
        if (ConstructionStrategy is not CollectionConstructionStrategy.Enumerable)
        {
            Throw();
            static void Throw() => throw new InvalidOperationException("The current dictionary shape does not support enumerable constructors.");
        }

        DebugExt.Assert(_enumerableCtor != null);

        // We'll use a shared delegate when no comparer applies.
        object? relevantComparer = GetRelevantComparer(collectionConstructionOptions);
        if (relevantComparer is null || _enumerableCtorWithComparer is null)
        {
            return _enumerableCtorDelegate ??= CreateEnumerableCtor();
            Func<IEnumerable<KeyValuePair<TKey, TValue>>, TDictionary> CreateEnumerableCtor()
            {
                if (_isFSharpMap)
                {
                    var mapOfSeqDelegate = ((MethodInfo)_enumerableCtor).CreateDelegate<Func<IEnumerable<Tuple<TKey, TValue>>, TDictionary>>();
                    return kvps => mapOfSeqDelegate(kvps.Select(kvp => new Tuple<TKey, TValue>(kvp.Key, kvp.Value)));
                }

                return _enumerableCtor switch
                {
                    ConstructorInfo ctorInfo => Provider.MemberAccessor.CreateFuncDelegate<IEnumerable<KeyValuePair<TKey, TValue>>, TDictionary>(ctorInfo),
                    _ => ((MethodInfo)_enumerableCtor).CreateDelegate<Func<IEnumerable<KeyValuePair<TKey, TValue>>, TDictionary>>(),
                };
            }
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    public SpanConstructor<KeyValuePair<TKey, TValue>, TDictionary> GetSpanConstructor(in CollectionConstructionOptions<TKey> collectionConstructionOptions)
    {
        if (ConstructionStrategy is not CollectionConstructionStrategy.Span)
        {
            Throw();
            static void Throw() => throw new InvalidOperationException("The current enumerable shape does not support span constructors.");
        }

        // We'll use a shared delegate when no comparer applies.
        object? relevantComparer = GetRelevantComparer(collectionConstructionOptions);
        if (relevantComparer is null || _spanCtorWithComparer is null)
        {
            return _spanCtorDelegate ??= CreateSpanConstructor();
            SpanConstructor<KeyValuePair<TKey, TValue>, TDictionary> CreateSpanConstructor()
            {
                if (_dictionaryCtor is ConstructorInfo dictionaryCtor)
                {
                    var dictionaryCtorDelegate = Provider.MemberAccessor.CreateFuncDelegate<Dictionary<TKey, TValue>, TDictionary>(dictionaryCtor);
                    return span => dictionaryCtorDelegate(CollectionHelpers.CreateDictionary(span, null));
                }

                DebugExt.Assert(_spanCtor != null);
                if (_spanCtor is ConstructorInfo ctorInfo)
                {
                    return Provider.MemberAccessor.CreateSpanConstructorDelegate<KeyValuePair<TKey, TValue>, TDictionary>(ctorInfo);
                }

                MethodInfo methodInfo = (MethodInfo)_spanCtor;

                // ReadOnlySpan<KeyValuePair<TKey, TValue>>
                return methodInfo.CreateDelegate<SpanConstructor<KeyValuePair<TKey, TValue>, TDictionary>>();
            }
        }
        else
        {
            if (_dictionaryCtorWithComparer is ConstructorInfo dictionaryCtorWithComparer)
            {
                switch (relevantComparer)
                {
                    case IEqualityComparer<TKey> equalityComparer:
                        var dictionaryCtorDelegate = Provider.MemberAccessor.CreateFuncDelegate<Dictionary<TKey, TValue>, IEqualityComparer<TKey>, TDictionary>(dictionaryCtorWithComparer);
                        return span => dictionaryCtorDelegate(CollectionHelpers.CreateDictionary(span, equalityComparer), equalityComparer);
                    case IComparer<TKey> comparer:
                        var sortedDictionaryCtorDelegate = Provider.MemberAccessor.CreateFuncDelegate<SortedDictionary<TKey, TValue>, IComparer<TKey>, TDictionary>(dictionaryCtorWithComparer);
                        return span => sortedDictionaryCtorDelegate(CollectionHelpers.CreateSortedDictionary(span, comparer), comparer);
                    default:
                        throw new NotSupportedException();
                }
            }

            DebugExt.Assert(_spanCtor != null);
            if (_spanCtorWithComparer is ConstructorInfo ctorInfoWithComparer)
            {
                return _constructionStyle switch
                {
                    ConstructionWithComparer.None => Provider.MemberAccessor.CreateSpanConstructorDelegate<KeyValuePair<TKey, TValue>, TDictionary>(ctorInfoWithComparer),
                    ConstructionWithComparer.ValuesEqualityComparer => Provider.MemberAccessor.CreateSpanConstructorWithTrailingECDelegate<KeyValuePair<TKey, TValue>, TKey, TDictionary>(ctorInfoWithComparer, (IEqualityComparer<TKey>)relevantComparer),
                    ConstructionWithComparer.EqualityComparerValues => Provider.MemberAccessor.CreateSpanConstructorWithLeadingECDelegate<KeyValuePair<TKey, TValue>, TKey, TDictionary>(ctorInfoWithComparer, (IEqualityComparer<TKey>)relevantComparer),
                    ConstructionWithComparer.ValuesComparer => Provider.MemberAccessor.CreateSpanConstructorWithTrailingCDelegate<KeyValuePair<TKey, TValue>, TKey, TDictionary>(ctorInfoWithComparer, (IComparer<TKey>)relevantComparer),
                    ConstructionWithComparer.ComparerValues => Provider.MemberAccessor.CreateSpanConstructorWithLeadingCDelegate<KeyValuePair<TKey, TValue>, TKey, TDictionary>(ctorInfoWithComparer, (IComparer<TKey>)relevantComparer),
                    _ => throw new InvalidOperationException("The current dictionary shape does not support span constructors."),
                };
            }

            MethodInfo methodInfo = (MethodInfo)_spanCtor;
            ParameterInfo[] parameters = methodInfo.GetParameters();

            if (parameters.Length is 2)
            {
                // ReadOnlySpan<KeyValuePair<TKey, TValue>>, I[Equality]Comparer<TKey>
                throw new NotImplementedException();
                return methodInfo.CreateDelegate<SpanConstructor<KeyValuePair<TKey, TValue>, TDictionary>>();
            }

            throw new NotSupportedException();
        }
    }

    [MemberNotNull(nameof(_constructionStyle))]
    private CollectionConstructionStrategy DetermineConstructionStrategy()
    {
        // TODO resolve CollectionBuilderAttribute once added for Dictionary types

        Type comparerType = typeof(IComparer<>).MakeGenericType(typeof(TKey));
        Type equalityComparerType = typeof(IEqualityComparer<>).MakeGenericType(typeof(TKey));

        if (typeof(TDictionary).GetConstructor([]) is ConstructorInfo defaultCtor)
        {
            MethodInfo? addMethod = typeof(TDictionary).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m =>
                    m.Name is "set_Item" or "Add" &&
                    m.GetParameters() is [ParameterInfo key, ParameterInfo value] &&
                    key.ParameterType == typeof(TKey) && value.ParameterType == typeof(TValue))
                .OrderByDescending(m => m.Name) // Prefer set_Item over Add
                .FirstOrDefault();

            if (addMethod != null)
            {
                _defaultCtor = defaultCtor;
                _addMethod = addMethod;
                (_constructionStyle, _defaultCtorWithComparer) = FindComparerConstructorOverload(defaultCtor);
                return CollectionConstructionStrategy.Mutable;
            }
        }

        if (Provider.Options.UseReflectionEmit && typeof(TDictionary).GetConstructor([typeof(ReadOnlySpan<KeyValuePair<TKey, TValue>>)]) is ConstructorInfo spanCtor)
        {
            // Cannot invoke constructors with ROS parameters without Ref.Emit
            _spanCtor = spanCtor;
            (_constructionStyle, _spanCtorWithComparer) = FindComparerConstructorOverload(spanCtor);
            return CollectionConstructionStrategy.Span;
        }

        if (typeof(TDictionary).GetConstructor([typeof(IEnumerable<KeyValuePair<TKey, TValue>>)]) is ConstructorInfo enumerableCtor)
        {
            _enumerableCtor = enumerableCtor;
            (_constructionStyle, _enumerableCtor) = FindComparerConstructorOverload(enumerableCtor);
            return CollectionConstructionStrategy.Enumerable;
        }

        if (typeof(TDictionary).GetConstructors()
            .FirstOrDefault(ctor => ctor.GetParameters() is [{ ParameterType: Type { IsGenericType: true } paramTy }] && paramTy.IsAssignableFrom(typeof(Dictionary<TKey, TValue>)))
            is ConstructorInfo dictionaryCtor)
        {
            // Handle types with ctors accepting IDictionary or IReadOnlyDictionary such as ReadOnlyDictionary<TKey, TValue>
            _dictionaryCtor = dictionaryCtor;
            (_constructionStyle, _dictionaryCtorWithComparer) = FindComparerConstructorOverload(dictionaryCtor);
            return CollectionConstructionStrategy.Span;
        }

        if (typeof(TDictionary).IsInterface)
        {
            if (typeof(TDictionary).IsAssignableFrom(typeof(Dictionary<TKey, TValue>)))
            {
                // Handle IDictionary, IDictionary<TKey, TValue> and IReadOnlyDictionary<TKey, TValue> using Dictionary<TKey, TValue>
                MethodInfo? gm = typeof(CollectionHelpers).GetMethod(nameof(CollectionHelpers.CreateDictionary), BindingFlags.Public | BindingFlags.Static);
                _spanCtor = gm?.MakeGenericMethod(typeof(TKey), typeof(TValue));
                (_constructionStyle, _spanCtorWithComparer) = FindComparerConstructionOverload(_spanCtor);
                return _spanCtor != null ? CollectionConstructionStrategy.Span : CollectionConstructionStrategy.None;
            }

            if (typeof(TDictionary) == typeof(IDictionary))
            {
                // Handle IDictionary using Dictionary<object, object>
                Debug.Assert(typeof(TKey) == typeof(object) && typeof(TValue) == typeof(object));
                MethodInfo? gm = typeof(CollectionHelpers).GetMethod(nameof(CollectionHelpers.CreateDictionary), BindingFlags.Public | BindingFlags.Static);
                _spanCtor = gm?.MakeGenericMethod(typeof(object), typeof(object));
                (_constructionStyle, _spanCtorWithComparer) = FindComparerConstructionOverload(_spanCtor);
                return _spanCtor != null ? CollectionConstructionStrategy.Span : CollectionConstructionStrategy.None;
            }

            _constructionStyle = ConstructionWithComparer.None;
            return CollectionConstructionStrategy.None;
        }

        if (typeof(TDictionary) is { Name: "ImmutableDictionary`2", Namespace: "System.Collections.Immutable" })
        {
            Type? factoryType = typeof(TDictionary).Assembly.GetType("System.Collections.Immutable.ImmutableDictionary");
            _enumerableCtor = factoryType?.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name is "CreateRange")
                .Where(m => m.GetParameters() is [ParameterInfo p1, ParameterInfo p2] && p1.ParameterType.IsIEqualityComparer<TKey>() && p2.ParameterType.IsIEnumerable())
                .Select(m => m.MakeGenericMethod(typeof(TKey), typeof(TValue)))
                .FirstOrDefault();

            (_constructionStyle, _enumerableCtorWithComparer) = FindComparerConstructionOverload(_enumerableCtor);
            return _enumerableCtor != null ? CollectionConstructionStrategy.Enumerable : CollectionConstructionStrategy.None;
        }

        if (typeof(TDictionary) is { Name: "ImmutableSortedDictionary`2", Namespace: "System.Collections.Immutable" })
        {
            Type? factoryType = typeof(TDictionary).Assembly.GetType("System.Collections.Immutable.ImmutableSortedDictionary");
            _enumerableCtor = factoryType?.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name is "CreateRange")
                .Where(m => m.GetParameters() is [ParameterInfo p1, ParameterInfo p2] && p1.ParameterType.IsIEqualityComparer<TKey>() && p2.ParameterType.IsIEnumerable())
                .Select(m => m.MakeGenericMethod(typeof(TKey), typeof(TValue)))
                .FirstOrDefault();

            (_constructionStyle, _enumerableCtorWithComparer) = FindComparerConstructionOverload(_enumerableCtor);
            return _enumerableCtor != null ? CollectionConstructionStrategy.Enumerable : CollectionConstructionStrategy.None;
        }

        if (typeof(TDictionary) is { Name: "FSharpMap`2", Namespace: "Microsoft.FSharp.Collections" })
        {
            Type? module = typeof(TDictionary).Assembly.GetType("Microsoft.FSharp.Collections.MapModule");
            _enumerableCtor = module?.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name is "OfSeq")
                .Where(m => m.GetParameters() is [ParameterInfo p] && p.ParameterType.IsIEnumerable())
                .Select(m => m.MakeGenericMethod(typeof(TKey), typeof(TValue)))
                .FirstOrDefault();

            _isFSharpMap = _enumerableCtor != null;
            (_constructionStyle, _enumerableCtorWithComparer) = FindComparerConstructionOverload(_enumerableCtor);
            return _enumerableCtor != null ? CollectionConstructionStrategy.Enumerable : CollectionConstructionStrategy.None;
        }

        _constructionStyle = ConstructionWithComparer.None;
        return CollectionConstructionStrategy.None;

        (ConstructionWithComparer, ConstructorInfo?) FindComparerConstructorOverload(ConstructorInfo? nonComparerOverload)
        {
            var (comparer, overload) = FindComparerConstructionOverload(nonComparerOverload);
            return (comparer, (ConstructorInfo?)overload);
        }

        (ConstructionWithComparer, MethodBase?) FindComparerConstructionOverload(MethodBase? nonComparerOverload)
        {
            if (nonComparerOverload is null)
            {
                return default;
            }

            switch (nonComparerOverload.GetParameters())
            {
                case []:
                    foreach (MethodBase overload in EnumerateOverloads())
                    {
                        if (overload.GetParameters() is not [ParameterInfo onlyParameter])
                        {
                            continue;
                        }

                        switch (ToComparerConstruction(onlyParameter))
                        {
                            case ComparerConstruction.Comparer:
                                return (ConstructionWithComparer.Comparer, overload);
                            case ComparerConstruction.EqualityComparer:
                                return (ConstructionWithComparer.EqualityComparer, overload);
                        }
                    }

                    break;
                case [{ ParameterType: Type collectionType }]:
                    foreach (MethodBase overload in EnumerateOverloads())
                    {
                        if (overload.GetParameters() is not [ParameterInfo first, ParameterInfo second])
                        {
                            continue;
                        }

                        ConstructionWithComparer comparerType = IsAcceptableConstructorPair(first, second, CollectionConstructorParameterType.CollectionOfT);
                        if (comparerType != ConstructionWithComparer.None)
                        {
                            return (comparerType, overload);
                        }
                    }

                    break;
            }

            return (ConstructionWithComparer.None, null);

            IEnumerable<MethodBase> EnumerateOverloads()
            {
                if (nonComparerOverload is ConstructorInfo)
                {
                    foreach (ConstructorInfo ctor in typeof(TDictionary).GetConstructors())
                    {
                        yield return ctor;
                    }
                }
                else if (nonComparerOverload is MethodInfo nonComparerMethod)
                {
                    foreach (MethodInfo method in typeof(TDictionary).GetMethods(BindingFlags.Public | BindingFlags.Static))
                    {
                        if (method.Name != nonComparerOverload.Name || nonComparerMethod.IsGenericMethod ^ method.IsGenericMethod)
                        {
                            continue;
                        }

                        yield return method.IsGenericMethod ? method.MakeGenericMethod(nonComparerMethod.GetGenericArguments()) : method;
                    }
                }
            }

            ComparerConstruction ToComparerConstruction(ParameterInfo parameter) => ClassifyConstructorParameter(parameter) switch
            {
                CollectionConstructorParameterType.IComparerOfT => ComparerConstruction.Comparer,
                CollectionConstructorParameterType.IEqualityComparerOfT => ComparerConstruction.EqualityComparer,
                _ => ComparerConstruction.None,
            };
        }
    }

    private CollectionConstructorParameterType ClassifyConstructorParameter(ParameterInfo parameter)
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

    private ConstructionWithComparer IsAcceptableConstructorPair(ParameterInfo first, ParameterInfo second, CollectionConstructorParameterType collectionType)
        => IsAcceptableConstructorPair(ClassifyConstructorParameter(first), ClassifyConstructorParameter(second), collectionType);

    private object? GetRelevantComparer(in CollectionConstructionOptions<TKey> collectionConstructionOptions)
        => this.CustomComparerSupport switch
        {
            ComparerConstruction.Comparer => collectionConstructionOptions.Comparer,
            ComparerConstruction.EqualityComparer => collectionConstructionOptions.EqualityComparer,
            _ => null,
        };
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
