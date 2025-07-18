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

internal sealed class MethodCollectionConstructorInfo(
    MethodBase factory,
    CollectionConstructorParameter[] signature,
    CollectionComparerOptions comparerOptions,
    MethodInfo? addMethod)
    : CollectionConstructorInfo(addMethod is null ? CollectionConstructionStrategy.Parameterized : CollectionConstructionStrategy.Mutable, comparerOptions)
{
    public MethodBase Factory { get; } = factory;
    public CollectionConstructorParameter[] Signature { get; } = signature;
    public MethodInfo? AddMethod { get; } = addMethod;
}