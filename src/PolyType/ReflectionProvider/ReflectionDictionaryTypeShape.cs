using PolyType.Abstractions;
using PolyType.SourceGenModel;
using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace PolyType.ReflectionProvider;

[DebuggerTypeProxy(typeof(PolyType.Debugging.DictionaryTypeShapeDebugView))]
[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal abstract class ReflectionDictionaryTypeShape<TDictionary, TKey, TValue>(ReflectionTypeShapeProvider provider, ReflectionTypeShapeOptions options)
    : ReflectionTypeShape<TDictionary>(provider, options), IDictionaryTypeShape<TDictionary, TKey, TValue>
    where TKey : notnull
{
    private CollectionConstructorInfo? _constructorInfo;
    private MutableCollectionConstructor<TKey, TDictionary>? _mutableCtorDelegate;
    private DictionaryInserter<TDictionary, TKey, TValue>? _addInserter;
    private DictionaryInserter<TDictionary, TKey, TValue>? _setInserter;
    private DictionaryInserter<TDictionary, TKey, TValue>? _tryAddInserter;
    private ParameterizedCollectionConstructor<TKey, KeyValuePair<TKey, TValue>, TDictionary>? _spanCtorDelegate;

    private CollectionConstructorInfo ConstructorInfo
    {
        get => _constructorInfo ?? CommonHelpers.ExchangeIfNull(ref _constructorInfo, DetermineConstructorInfo());
    }

    public sealed override TypeShapeKind Kind => TypeShapeKind.Dictionary;
    public sealed override object? Accept(TypeShapeVisitor visitor, object? state = null) => visitor.VisitDictionary(this, state);

    public CollectionComparerOptions SupportedComparer => ConstructorInfo.ComparerOptions;
    public CollectionConstructionStrategy ConstructionStrategy => ConstructorInfo.Strategy;
    public DictionaryInsertionMode AvailableInsertionModes =>
        ConstructorInfo is MutableCollectionConstructorInfo mutableCtor
        ? mutableCtor.AvailableInsertionModes
        : DictionaryInsertionMode.None;

    public ITypeShape<TKey> KeyType => Provider.GetTypeShape<TKey>();
    public ITypeShape<TValue> ValueType => Provider.GetTypeShape<TValue>();
    ITypeShape IDictionaryTypeShape.KeyType => KeyType;
    ITypeShape IDictionaryTypeShape.ValueType => ValueType;

    public abstract Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> GetGetDictionary();

    public DictionaryInserter<TDictionary, TKey, TValue> GetInserter(DictionaryInsertionMode insertionMode)
    {
        if (ConstructionStrategy is not CollectionConstructionStrategy.Mutable)
        {
            Throw();
            static void Throw() => throw new InvalidOperationException("The current dictionary shape does not support mutation.");
        }

        switch (DetermineInsertionMode(AvailableInsertionModes, insertionMode))
        {
            case DictionaryInsertionMode.Overwrite: return _setInserter ?? CreateInserter(ref _setInserter, DictionaryInsertionMode.Overwrite);
            case DictionaryInsertionMode.Discard: return _tryAddInserter ?? CreateInserter(ref _tryAddInserter, DictionaryInsertionMode.Discard);
            case DictionaryInsertionMode.Throw: return _addInserter ?? CreateInserter(ref _addInserter, DictionaryInsertionMode.Throw);
            default:
                Throw();
                return null!;
                static void Throw() => throw new ArgumentOutOfRangeException(nameof(insertionMode), "The requested insertion mode is not supported by the current dictionary shape.");
        }

        DictionaryInserter<TDictionary, TKey, TValue> CreateInserter(ref DictionaryInserter<TDictionary, TKey, TValue>? field, DictionaryInsertionMode insertionMode)
        {
            DebugExt.Assert(_constructorInfo is MutableCollectionConstructorInfo);
            var inserter = Provider.MemberAccessor.CreateDictionaryInserter<TDictionary, TKey, TValue>((MutableCollectionConstructorInfo)_constructorInfo, insertionMode);
            return CommonHelpers.ExchangeIfNull(ref field, inserter);
        }
    }

    public MutableCollectionConstructor<TKey, TDictionary> GetDefaultConstructor()
    {
        if (ConstructionStrategy is not CollectionConstructionStrategy.Mutable)
        {
            Throw();
            static void Throw() => throw new InvalidOperationException("The current dictionary shape does not support mutation.");
        }

        return _mutableCtorDelegate ?? CommonHelpers.ExchangeIfNull(ref _mutableCtorDelegate, CreateMutableCtor());

        MutableCollectionConstructor<TKey, TDictionary> CreateMutableCtor()
        {
            DebugExt.Assert(_constructorInfo is MutableCollectionConstructorInfo);
            return Provider.MemberAccessor.CreateMutableCollectionConstructor<TKey, TValue, TDictionary>((MutableCollectionConstructorInfo)_constructorInfo);
        }
    }

    public ParameterizedCollectionConstructor<TKey, KeyValuePair<TKey, TValue>, TDictionary> GetParameterizedConstructor()
    {
        if (ConstructionStrategy is not CollectionConstructionStrategy.Parameterized)
        {
            Throw();
            static void Throw() => throw new InvalidOperationException("The current dictionary shape does not support parameterized constructors.");
        }

        return _spanCtorDelegate ?? CommonHelpers.ExchangeIfNull(ref _spanCtorDelegate, CreateParameterizedCtor());
        ParameterizedCollectionConstructor<TKey, KeyValuePair<TKey, TValue>, TDictionary> CreateParameterizedCtor()
        {
            DebugExt.Assert(_constructorInfo is ParameterizedCollectionConstructorInfo);
            return Provider.MemberAccessor.CreateParameterizedCollectionConstructor<TKey, KeyValuePair<TKey, TValue>, TDictionary>((ParameterizedCollectionConstructorInfo)_constructorInfo);
        }
    }

    private CollectionConstructorInfo DetermineConstructorInfo()
    {
        Type dictionaryType = typeof(TDictionary);

        // The System.Collections.Generic.Dictionary<TKey, TValue> type corresponding to the current shape.
        Type correspondingGenericDictionaryType = typeof(Dictionary<TKey, TValue>);

        // Used by the F# map factory method.
        Type correspondingTupleEnumerableType = typeof(IEnumerable<Tuple<TKey, TValue>>);

        if (GetImmutableDictionaryFactory() is { } factoryCtorInfo)
        {
            return factoryCtorInfo;
        }

        ConstructorInfo[] allCtors = DetermineImplementationType(dictionaryType).GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        DictionaryInsertionMode availableInsertionModes = ResolveInsertionMethods(
            typeof(TDictionary),
            out MethodInfo? addMethod,
            out MethodInfo? setMethod,
            out MethodInfo? tryAddMethod,
            out MethodInfo? containsKeyMethod);

        if (Provider.ResolveBestCollectionCtor<KeyValuePair<TKey, TValue>, TKey>(
                dictionaryType,
                allCtors,
                addMethod,
                setMethod,
                tryAddMethod,
                containsKeyMethod,
                availableInsertionModes,
                correspondingGenericDictionaryType,
                correspondingTupleEnumerableType) is { } collectionCtorInfo)
        {
            return collectionCtorInfo;
        }

        // Check for CollectionBuilderAttribute as the last option.
        if (Provider.ResolveBestCollectionCtor<KeyValuePair<TKey, TValue>, TKey>(
                dictionaryType,
                dictionaryType.GetCollectionBuilderAttributeMethods(typeof(KeyValuePair<TKey, TValue>)),
                addMethod: null,
                setMethod: null,
                tryAddMethod: null,
                containsKeyMethod: null,
                insertionMode: DictionaryInsertionMode.None,
                correspondingGenericDictionaryType,
                correspondingTupleEnumerableType) is { } builderCtorInfo)
        {
            return builderCtorInfo;
        }

        return NoCollectionConstructorInfo.Instance;

        CollectionConstructorInfo? GetImmutableDictionaryFactory()
        {
            if (dictionaryType == typeof(IReadOnlyDictionary<TKey, TValue>))
            {
                return ResolveFactoryMethod("PolyType.SourceGenModel.CollectionHelpers", "CreateDictionary");
            }

            if (dictionaryType is { Name: "ImmutableDictionary`2", Namespace: "System.Collections.Immutable" }
                   or { Name: "IImmutableDictionary`2", Namespace: "System.Collections.Immutable" })
            {
                return ResolveFactoryMethod("System.Collections.Immutable.ImmutableDictionary");
            }

            if (dictionaryType is { Name: "ImmutableSortedDictionary`2", Namespace: "System.Collections.Immutable" })
            {
                return ResolveFactoryMethod("System.Collections.Immutable.ImmutableSortedDictionary");
            }

            if (dictionaryType is { Name: "FrozenDictionary`2", Namespace: "System.Collections.Frozen" })
            {
                return ResolveFactoryMethod("System.Collections.Frozen.FrozenDictionary", "ToFrozenDictionary");
            }

            if (dictionaryType is { Name: "FSharpMap`2", Namespace: "Microsoft.FSharp.Collections" })
            {
                return ResolveFactoryMethod("Microsoft.FSharp.Collections.MapModule", "OfSeq");
            }

            return null;

            CollectionConstructorInfo ResolveFactoryMethod(string typeName, string? factoryName = null)
            {
                Type? factoryType = dictionaryType.Assembly.GetType(typeName) ?? Assembly.GetExecutingAssembly().GetType(typeName);
                if (factoryType is not null)
                {
                    var candidates = factoryType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                        .Where(m => factoryName is null ? m.Name is "Create" or "CreateRange" : m.Name == factoryName)
                        .Where(m => m.GetGenericArguments().Length == 2)
                        .Select(m => m.MakeGenericMethod(typeof(TKey), typeof(TValue)));

                    if (Provider.ResolveBestCollectionCtor<KeyValuePair<TKey, TValue>, TKey>(
                            dictionaryType,
                            candidates,
                            addMethod: null,
                            correspondingDictionaryType: correspondingGenericDictionaryType,
                            correspondingTupleEnumerableType: correspondingTupleEnumerableType) is { } factoryCtorInfo)
                    {
                        return factoryCtorInfo;
                    }
                }

                return NoCollectionConstructorInfo.Instance;
            }
        }

        static DictionaryInsertionMode ResolveInsertionMethods(
            Type dictionaryType,
            out MethodInfo? addMethod,
            out MethodInfo? setMethod,
            out MethodInfo? tryAddMethod,
            out MethodInfo? containsKeyMethod)
        {
            IEnumerable<MethodInfo> instanceMethods = dictionaryType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
            if (!dictionaryType.IsInterface)
            {
                if (typeof(IDictionary<TKey, TValue>).IsAssignableFrom(dictionaryType))
                {
                    instanceMethods = instanceMethods.Concat(typeof(IDictionary<TKey, TValue>).GetMethods());
                }
                else if (typeof(IDictionary).IsAssignableFrom(dictionaryType))
                {
                    instanceMethods = instanceMethods.Concat(typeof(IDictionary).GetMethods());
                }
            }

            DictionaryInsertionMode availableModes = DictionaryInsertionMode.None;

            addMethod = instanceMethods
                .Where(m =>
                    m.Name is "Add" &&
                    m.GetParameters() is [ParameterInfo key, ParameterInfo value] &&
                    key.ParameterType == typeof(TKey) && value.ParameterType == typeof(TValue))
                .FirstOrDefault();

            if (addMethod is not null)
            {
                availableModes |= DictionaryInsertionMode.Throw;
            }

            setMethod = instanceMethods
                .Where(m =>
                    m.Name is "set_Item" &&
                    m.GetParameters() is [ParameterInfo key, ParameterInfo value] &&
                    key.ParameterType == typeof(TKey) && value.ParameterType == typeof(TValue))
                .FirstOrDefault();

            if (setMethod is not null)
            {
                availableModes |= DictionaryInsertionMode.Overwrite;
            }

            tryAddMethod = instanceMethods
                .Where(m =>
                    m.Name is "TryAdd" &&
                    m.GetParameters() is [ParameterInfo key, ParameterInfo value] &&
                    m.ReturnType == typeof(bool) &&
                    key.ParameterType == typeof(TKey) && value.ParameterType == typeof(TValue))
                .FirstOrDefault();

            if (tryAddMethod is not null)
            {
                availableModes |= DictionaryInsertionMode.Discard;
                containsKeyMethod = null;
            }
            else if (addMethod is not null || setMethod is not null)
            {
                // If TryAdd is not available, check if a ContainsKey/Add combination is available.
                containsKeyMethod = instanceMethods
                    .Where(m =>
                        m.Name is "ContainsKey" or "Contains" &&
                        m.GetParameters() is [ParameterInfo key] &&
                        m.ReturnType == typeof(bool) &&
                        key.ParameterType == typeof(TKey))
                    .FirstOrDefault();

                if (containsKeyMethod is not null)
                {
                    availableModes |= DictionaryInsertionMode.Discard;
                }
            }
            else
            {
                containsKeyMethod = null;
            }

            return availableModes;
        }
    }

    private static Type DetermineImplementationType(Type dictionaryType)
    {
        if (dictionaryType.IsInterface)
        {
            if (dictionaryType == typeof(IDictionary<TKey, TValue>) ||
                dictionaryType == typeof(IDictionary))
            {
                // Handle IDictionary<TKey, TValue> and IDictionary using Dictionary<TKey, TValue>
                return typeof(Dictionary<TKey, TValue>);
            }
        }

        return dictionaryType;
    }

    private static DictionaryInsertionMode DetermineInsertionMode(DictionaryInsertionMode supportedModes, DictionaryInsertionMode requestedMode)
    {
        Debug.Assert(supportedModes is not DictionaryInsertionMode.None);
        switch (requestedMode)
        {
            case DictionaryInsertionMode.None:
                // If no specific mode is requested, return the first supported mode.
                ReadOnlySpan<DictionaryInsertionMode> allModes = [
                    DictionaryInsertionMode.Overwrite,
                    DictionaryInsertionMode.Discard,
                    DictionaryInsertionMode.Throw
                ];

                foreach (var mode in allModes)
                {
                    if ((supportedModes & mode) != 0)
                    {
                        return mode;
                    }
                }

                break;

            case DictionaryInsertionMode.Overwrite when (supportedModes & DictionaryInsertionMode.Overwrite) != 0:
            case DictionaryInsertionMode.Discard when (supportedModes & DictionaryInsertionMode.Discard) != 0:
            case DictionaryInsertionMode.Throw when (supportedModes & DictionaryInsertionMode.Throw) != 0:
                return requestedMode;
        }

        return DictionaryInsertionMode.None;
    }
}

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class ReflectionDictionaryOfTShape<TDictionary, TKey, TValue>(ReflectionTypeShapeProvider provider, ReflectionTypeShapeOptions options)
    : ReflectionDictionaryTypeShape<TDictionary, TKey, TValue>(provider, options)
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
internal sealed class ReflectionReadOnlyDictionaryShape<TDictionary, TKey, TValue>(ReflectionTypeShapeProvider provider, ReflectionTypeShapeOptions options)
    : ReflectionDictionaryTypeShape<TDictionary, TKey, TValue>(provider, options)
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
internal sealed class ReflectionNonGenericDictionaryShape<TDictionary>(ReflectionTypeShapeProvider provider, ReflectionTypeShapeOptions options)
    : ReflectionDictionaryTypeShape<TDictionary, object, object?>(provider, options)
    where TDictionary : IDictionary
{
    public override Func<TDictionary, IReadOnlyDictionary<object, object?>> GetGetDictionary()
    {
        return static obj => CollectionHelpers.AsReadOnlyDictionary(obj);
    }
}
