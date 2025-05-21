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

    protected static CollectionComparerOptions ToComparerConstruction(ConstructionWithComparer signature)
        => signature switch
        {
            ConstructionWithComparer.Comparer or ConstructionWithComparer.ComparerValues or ConstructionWithComparer.ValuesComparer => CollectionComparerOptions.Comparer,
            ConstructionWithComparer.EqualityComparer or ConstructionWithComparer.EqualityComparerValues or ConstructionWithComparer.ValuesEqualityComparer => CollectionComparerOptions.EqualityComparer,
            ConstructionWithComparer.None => CollectionComparerOptions.None,
            _ => throw new NotImplementedException(),
        };

    protected static object? GetRelevantComparer<TKey>(CollectionConstructionOptions<TKey>? collectionConstructionOptions, CollectionComparerOptions customComparerConstruction)
        => customComparerConstruction switch
        {
            CollectionComparerOptions.Comparer => collectionConstructionOptions?.Comparer,
            CollectionComparerOptions.EqualityComparer => collectionConstructionOptions?.EqualityComparer,
            _ => null,
        };

    protected static Func<object, SpanConstructor<TElement, TResult>> CreateSpanMethodDelegate<TElement, TCompare, TResult>(MethodInfo methodInfo, ConstructionWithComparer signatureStyle)
    {
        switch (signatureStyle)
        {
            case ConstructionWithComparer.ValuesEqualityComparer:
                var ctor = methodInfo.CreateDelegate<SpanECConstructor<TElement, TCompare, TResult>>();
                return comparer => values => ctor(values, (IEqualityComparer<TCompare>)comparer);
            case ConstructionWithComparer.EqualityComparerValues:
                var ctor2 = methodInfo.CreateDelegate<ECSpanConstructor<TElement, TCompare, TResult>>();
                return comparer => values => ctor2((IEqualityComparer<TCompare>)comparer, values);
            case ConstructionWithComparer.ValuesComparer:
                var ctor3 = methodInfo.CreateDelegate<SpanCConstructor<TElement, TCompare, TResult>>();
                return comparer => values => ctor3(values, (IComparer<TCompare>)comparer);
            case ConstructionWithComparer.ComparerValues:
                var ctor4 = methodInfo.CreateDelegate<CSpanConstructor<TElement, TCompare, TResult>>();
                return comparer => values => ctor4((IComparer<TCompare>)comparer, values);
            default:
                throw new NotSupportedException();
        }
    }

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
                        case CollectionComparerOptions.Comparer:
                            return (ConstructionWithComparer.Comparer, overload);
                        case CollectionComparerOptions.EqualityComparer:
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

    protected CollectionComparerOptions ToComparerConstruction(ParameterInfo parameter) => ClassifyConstructorParameter(parameter) switch
    {
        CollectionConstructorParameterType.IComparerOfT => CollectionComparerOptions.Comparer,
        CollectionConstructorParameterType.IEqualityComparerOfT => CollectionComparerOptions.EqualityComparer,
        _ => CollectionComparerOptions.None,
    };

    protected virtual CollectionConstructorParameterType ClassifyConstructorParameter(ParameterInfo parameter) => throw new NotImplementedException();

    protected enum CollectionConstructorParameterType
    {
        /// <summary>
        /// The parameter isn't a recognized type.
        /// </summary>
        Unrecognized,

        /// <summary>
        /// The parameter serves as some type of collection.
        /// </summary>
        CollectionOfT,

        /// <summary>
        /// The parameter is an <see cref="IEqualityComparer{T}"/>.
        /// </summary>
        IEqualityComparerOfT,

        /// <summary>
        /// The parameter is an <see cref="IComparer{T}"/>.
        /// </summary>
        IComparerOfT,
    }
}
