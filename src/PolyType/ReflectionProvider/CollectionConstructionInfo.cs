using PolyType.Abstractions;
using System.Globalization;
using System.Reflection;

namespace PolyType.ReflectionProvider;

internal abstract class CollectionConstructorInfo
{
    public static NoCollectionConstructorInfo NoConstructor { get; } = new();
    public abstract CollectionConstructionStrategy Strategy { get; }
    public abstract CollectionComparerOptions ComparerOptions { get; }

    protected static CollectionComparerOptions DetermineComparerOptions(CollectionConstructorParameter[] signature)
    {
        bool hasEqualityComparer = false;
        bool hasComparer = false;

        foreach (CollectionConstructorParameter parameterType in signature)
        {
            switch (parameterType)
            {
                case CollectionConstructorParameter.EqualityComparer:
                case CollectionConstructorParameter.EqualityComparerOptional:
                case CollectionConstructorParameter.Dictionary:
                case CollectionConstructorParameter.HashSet:
                    hasEqualityComparer = true;
                    break;

                case CollectionConstructorParameter.Comparer:
                case CollectionConstructorParameter.ComparerOptional:
                    hasComparer = true;
                    break;
            }
        }

        return hasEqualityComparer ? CollectionComparerOptions.EqualityComparer :
            hasComparer ? CollectionComparerOptions.Comparer :
            CollectionComparerOptions.None;
    }
}

internal sealed class NoCollectionConstructorInfo : CollectionConstructorInfo
{
    public override CollectionConstructionStrategy Strategy => CollectionConstructionStrategy.None;
    public override CollectionComparerOptions ComparerOptions => CollectionComparerOptions.None;
}

internal sealed class MutableCollectionConstructorInfo(
    ConstructorInfo defaultCtor,
    CollectionConstructorParameter[] signature,
    MethodInfo addMethod)
    : CollectionConstructorInfo
{
    public override CollectionConstructionStrategy Strategy => CollectionConstructionStrategy.Mutable;
    public override CollectionComparerOptions ComparerOptions { get; } = DetermineComparerOptions(signature);
    public ConstructorInfo DefaultConstructor { get; } = defaultCtor;
    public CollectionConstructorParameter[] Signature { get; } = signature;
    public MethodInfo AddMethod { get; } = addMethod;
}

internal sealed class ParameterizedCollectionConstructorInfo(
    MethodBase factory,
    CollectionConstructionStrategy strategy,
    CollectionConstructorParameter[] signature,
    bool isFSharpMap = false)
    : CollectionConstructorInfo
{
    public override CollectionConstructionStrategy Strategy { get; } = strategy;
    public override CollectionComparerOptions ComparerOptions { get; } = DetermineComparerOptions(signature);
    public MethodBase Factory { get; } = factory;
    public CollectionConstructorParameter[] Signature { get; } = signature;
    public bool IsFSharpMap { get; } = isFSharpMap;
}