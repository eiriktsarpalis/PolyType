using PolyType.Abstractions;
using System.Globalization;
using System.Reflection;

namespace PolyType.ReflectionProvider;

internal abstract class CollectionConstructorInfo(CollectionConstructionStrategy strategy, CollectionComparerOptions comparerOptions)
{
    public CollectionConstructionStrategy Strategy { get; } = strategy;
    public CollectionComparerOptions ComparerOptions { get; } = comparerOptions;
}

internal sealed class NoCollectionConstructorInfo() : CollectionConstructorInfo(CollectionConstructionStrategy.None, CollectionComparerOptions.None)
{
    public static NoCollectionConstructorInfo Instance { get; } = new();
}

internal abstract class MethodCollectionConstructorInfo(
    MethodBase factory,
    CollectionConstructorParameter[] signature,
    CollectionConstructionStrategy strategy,
    CollectionComparerOptions comparerOptions)
    : CollectionConstructorInfo(strategy, comparerOptions)
{
    public MethodBase Factory { get; } = factory;
    public CollectionConstructorParameter[] Signature { get; } = signature;
}

internal sealed class MutableCollectionConstructorInfo(
    MethodBase factory,
    CollectionConstructorParameter[] signature,
    MethodInfo? addMethod,
    MethodInfo? setMethod,
    MethodInfo? tryAddMethod,
    MethodInfo? containsKeyMethod,
    DictionaryInsertionMode insertionModes,
    CollectionComparerOptions comparerOptions)
    : MethodCollectionConstructorInfo(factory, signature, CollectionConstructionStrategy.Mutable, comparerOptions)
{
    public MethodInfo? AddMethod { get; } = addMethod;
    public MethodInfo? TryAddMethod { get; } = tryAddMethod;
    public MethodInfo? SetMethod { get; } = setMethod;
    public MethodInfo? ContainsKeyMethod { get; } = containsKeyMethod;
    public DictionaryInsertionMode AvailableInsertionModes { get; } = insertionModes;
}

internal sealed class ParameterizedCollectionConstructorInfo(
    MethodBase factory,
    CollectionConstructorParameter[] signature,
    CollectionComparerOptions comparerOptions)
    : MethodCollectionConstructorInfo(factory, signature, CollectionConstructionStrategy.Parameterized, comparerOptions);