using PolyType.Abstractions;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace PolyType.ReflectionProvider;

[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
internal abstract class ReflectionTypeShape<T>(ReflectionTypeShapeProvider provider) : ITypeShape<T>
{
    public abstract TypeShapeKind Kind { get; }
    public abstract object? Accept(TypeShapeVisitor visitor, object? state = null);
    public ReflectionTypeShapeProvider Provider => provider;
    public Type Type => typeof(T);

    ITypeShapeProvider ITypeShape.Provider => provider;
    ICustomAttributeProvider? ITypeShape.AttributeProvider => typeof(T);
    object? ITypeShape.Invoke(ITypeShapeFunc func, object? state) => func.Invoke(this, state);

    public ITypeShape? GetAssociatedTypeShape(Type associatedType)
    {
        if (associatedType.IsGenericTypeDefinition && this.Type.GenericTypeArguments.Length != associatedType.GetTypeInfo().GenericTypeParameters.Length)
        {
            throw new ArgumentException($"Related type arity ({associatedType.GenericTypeArguments.Length}) mismatch with original type ({this.Type.GenericTypeArguments.Length}).");
        }

        Type closedType = associatedType.IsGenericTypeDefinition
            ? associatedType.MakeGenericType(this.Type.GenericTypeArguments)
            : associatedType;

        return provider.GetShape(closedType);
    }

    protected static ConstructionWithComparer IsAcceptableConstructorPair(CollectionConstructorParameterType first, CollectionConstructorParameterType second, CollectionConstructorParameterType collectionType)
    {
        return (first, second) switch
        {
            (CollectionConstructorParameterType.IComparerOfT, CollectionConstructorParameterType.CollectionOfT) => ConstructionWithComparer.ComparerValues,
            (CollectionConstructorParameterType.CollectionOfT, CollectionConstructorParameterType.IComparerOfT) => ConstructionWithComparer.ValuesComparer,
            (CollectionConstructorParameterType.IEqualityComparerOfT, CollectionConstructorParameterType.CollectionOfT) => ConstructionWithComparer.EqualityComparerValues,
            (CollectionConstructorParameterType.CollectionOfT, CollectionConstructorParameterType.IEqualityComparerOfT) => ConstructionWithComparer.ValuesEqualityComparer,
            _ => ConstructionWithComparer.None,
        };
    }
    protected static ComparerConstruction ToComparerConstruction(ConstructionWithComparer signature)
        => signature switch
        {
            ConstructionWithComparer.Comparer or ConstructionWithComparer.ComparerValues or ConstructionWithComparer.ValuesComparer => ComparerConstruction.Comparer,
            ConstructionWithComparer.EqualityComparer or ConstructionWithComparer.EqualityComparerValues or ConstructionWithComparer.ValuesEqualityComparer => ComparerConstruction.EqualityComparer,
            ConstructionWithComparer.None => ComparerConstruction.None,
            _ => throw new NotImplementedException(),
        };

    protected (ConstructionWithComparer, ConstructorInfo?) FindComparerConstructorOverload(ConstructorInfo? nonComparerOverload)
    {
        var (comparer, overload) = FindComparerConstructionOverload(nonComparerOverload);
        return (comparer, (ConstructorInfo?)overload);
    }

    protected ConstructionWithComparer IsAcceptableConstructorPair(ParameterInfo first, ParameterInfo second, CollectionConstructorParameterType collectionType)
        => IsAcceptableConstructorPair(ClassifyConstructorParameter(first), ClassifyConstructorParameter(second), collectionType);

    protected (ConstructionWithComparer, MethodBase?) FindComparerConstructionOverload(MethodBase? nonComparerOverload)
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
            if (nonComparerOverload is ConstructorInfo { DeclaringType: not null })
            {
                foreach (ConstructorInfo ctor in nonComparerOverload.DeclaringType.GetConstructors())
                {
                    yield return ctor;
                }
            }
            else if (nonComparerOverload is MethodInfo { DeclaringType: { } declaringType } nonComparerMethod)
            {
                foreach (MethodInfo method in declaringType.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    if (method.Name != nonComparerMethod.Name || nonComparerMethod.IsGenericMethod ^ method.IsGenericMethod)
                    {
                        continue;
                    }

                    yield return method.IsGenericMethod ? method.MakeGenericMethod(nonComparerMethod.GetGenericArguments()) : method;
                }
            }
        }
    }

    protected object? GetRelevantComparer<TKey>(in CollectionConstructionOptions<TKey> collectionConstructionOptions, ComparerConstruction customComparerConstruction)
        => customComparerConstruction switch
        {
            ComparerConstruction.Comparer => collectionConstructionOptions.Comparer,
            ComparerConstruction.EqualityComparer => collectionConstructionOptions.EqualityComparer,
            _ => null,
        };

    protected ComparerConstruction ToComparerConstruction(ParameterInfo parameter) => ClassifyConstructorParameter(parameter) switch
    {
        CollectionConstructorParameterType.IComparerOfT => ComparerConstruction.Comparer,
        CollectionConstructorParameterType.IEqualityComparerOfT => ComparerConstruction.EqualityComparer,
        _ => ComparerConstruction.None,
    };

    protected virtual CollectionConstructorParameterType ClassifyConstructorParameter(ParameterInfo parameter) => throw new NotImplementedException();

    protected enum CollectionConstructorParameterType
    {
        Unrecognized,
        CollectionOfT,
        IEqualityComparerOfT,
        IComparerOfT,
    }
}
