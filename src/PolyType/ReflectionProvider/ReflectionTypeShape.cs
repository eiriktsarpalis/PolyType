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

    protected static ConstructionSignature IsAcceptableConstructorPair(CollectionConstructorParameterType first, CollectionConstructorParameterType second, CollectionConstructorParameterType collectionType)
    {
        return (first, second) switch
        {
            (CollectionConstructorParameterType.IComparerOfT, CollectionConstructorParameterType.CollectionOfT) => ConstructionSignature.ComparerValues,
            (CollectionConstructorParameterType.CollectionOfT, CollectionConstructorParameterType.IComparerOfT) => ConstructionSignature.ValuesComparer,
            (CollectionConstructorParameterType.IEqualityComparerOfT, CollectionConstructorParameterType.CollectionOfT) => ConstructionSignature.EqualityComparerValues,
            (CollectionConstructorParameterType.CollectionOfT, CollectionConstructorParameterType.IEqualityComparerOfT) => ConstructionSignature.ValuesEqualityComparer,
            _ => ConstructionSignature.None,
        };
    }

    protected static CollectionComparerOptions ToComparerConstruction(ConstructionSignature signature)
        => signature switch
        {
            ConstructionSignature.Comparer or ConstructionSignature.ComparerValues or ConstructionSignature.ValuesComparer => CollectionComparerOptions.Comparer,
            ConstructionSignature.EqualityComparer or ConstructionSignature.EqualityComparerValues or ConstructionSignature.ValuesEqualityComparer => CollectionComparerOptions.EqualityComparer,
            ConstructionSignature.None => CollectionComparerOptions.None,
            _ => throw new NotImplementedException(),
        };

    protected static object? GetRelevantComparer<TKey>(CollectionConstructionOptions<TKey>? collectionConstructionOptions, CollectionComparerOptions customComparerConstruction)
        => customComparerConstruction switch
        {
            CollectionComparerOptions.Comparer => collectionConstructionOptions?.Comparer,
            CollectionComparerOptions.EqualityComparer => collectionConstructionOptions?.EqualityComparer,
            _ => null,
        };

    protected Func<T> CreateConstructorDelegate(MethodBase methodBase)
    {
        return methodBase switch
        {
            MethodInfo methodInfo => methodInfo.CreateDelegate<Func<T>>(),
            ConstructorInfo ctorInfo => provider.MemberAccessor.CreateDefaultConstructor<T>(new MethodConstructorShapeInfo(typeof(T), methodBase, [])),
            _ => throw new NotSupportedException($"Method base of type {methodBase.GetType()} is not supported for creating a delegate."),
        };
    }

    protected SpanOnlyConstructor<TElement, T> CreateSpanConstructorDelegate<TElement>(MethodBase methodBase)
    {
        return methodBase switch
        {
            MethodInfo methodInfo => methodInfo.CreateDelegate<SpanOnlyConstructor<TElement, T>>(),
            ConstructorInfo ctorInfo => provider.MemberAccessor.CreateFuncDelegate<SpanOnlyConstructor<TElement, T>>(ctorInfo),
            _ => throw new NotSupportedException($"Method base of type {methodBase.GetType()} is not supported for creating a delegate."),
        };
    }

    protected SpanECConstructor<TKey, TElement, T> CreateSpanECConstructorDelegate<TKey, TElement>(MethodBase methodBase)
    {
        return methodBase switch
        {
            MethodInfo methodInfo => methodInfo.CreateDelegate<SpanECConstructor<TKey, TElement, T>>(),
            ConstructorInfo ctorInfo => provider.MemberAccessor.CreateFuncDelegate<SpanECConstructor<TKey, TElement, T>>(ctorInfo),
            _ => throw new NotSupportedException($"Method base of type {methodBase.GetType()} is not supported for creating a delegate."),
        };
    }

    protected SpanCConstructor<TKey, TElement, T> CreateSpanCConstructorDelegate<TKey, TElement>(MethodBase methodBase)
    {
        return methodBase switch
        {
            MethodInfo methodInfo => methodInfo.CreateDelegate<SpanCConstructor<TKey, TElement, T>>(),
            ConstructorInfo ctorInfo => provider.MemberAccessor.CreateFuncDelegate<SpanCConstructor<TKey, TElement, T>>(ctorInfo),
            _ => throw new NotSupportedException($"Method base of type {methodBase.GetType()} is not supported for creating a delegate."),
        };
    }

    protected ECSpanConstructor<TKey, TElement, T> CreateECSpanConstructorDelegate<TKey, TElement>(MethodBase methodBase)
    {
        return methodBase switch
        {
            MethodInfo methodInfo => methodInfo.CreateDelegate<ECSpanConstructor<TKey, TElement, T>>(),
            ConstructorInfo ctorInfo => provider.MemberAccessor.CreateFuncDelegate<ECSpanConstructor<TKey, TElement, T>>(ctorInfo),
            _ => throw new NotSupportedException($"Method base of type {methodBase.GetType()} is not supported for creating a delegate."),
        };
    }

    protected CSpanConstructor<TKey, TElement, T> CreateCSpanConstructorDelegate<TKey, TElement>(MethodBase methodBase)
    {
        return methodBase switch
        {
            MethodInfo methodInfo => methodInfo.CreateDelegate<CSpanConstructor<TKey, TElement, T>>(),
            ConstructorInfo ctorInfo => provider.MemberAccessor.CreateFuncDelegate<CSpanConstructor<TKey, TElement, T>>(ctorInfo),
            _ => throw new NotSupportedException($"Method base of type {methodBase.GetType()} is not supported for creating a delegate."),
        };
    }

    protected Func<TArg, T> CreateConstructorDelegate<TArg>(MethodBase methodBase)
    {
        return methodBase switch
        {
            MethodInfo methodInfo => methodInfo.CreateDelegate<Func<TArg, T>>(),
            ConstructorInfo ctorInfo => provider.MemberAccessor.CreateFuncDelegate<TArg, T>(ctorInfo),
            _ => throw new NotSupportedException($"Method base of type {methodBase.GetType()} is not supported for creating a delegate."),
        };
    }

    protected Func<TArg1, TArg2, T> CreateConstructorDelegate<TArg1, TArg2>(MethodBase methodBase)
    {
        return methodBase switch
        {
            MethodInfo methodInfo => methodInfo.CreateDelegate<Func<TArg1, TArg2, T>>(),
            ConstructorInfo ctorInfo => provider.MemberAccessor.CreateFuncDelegate<TArg1, TArg2, T>(ctorInfo),
            _ => throw new NotSupportedException($"Method base of type {methodBase.GetType()} is not supported for creating a delegate."),
        };
    }

    protected ConstructionSignature IsAcceptableConstructorPair(ParameterInfo first, ParameterInfo second, CollectionConstructorParameterType collectionType)
        => IsAcceptableConstructorPair(ClassifyConstructorParameter(first), ClassifyConstructorParameter(second), collectionType);

    protected (MethodBase, ConstructionSignature)? FindComparerConstructionOverload(MethodBase nonComparerOverload, ConstructionSignature nonComparerSignature)
    {
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
                            return (overload, ConstructionSignature.Comparer);
                        case CollectionComparerOptions.EqualityComparer:
                            return (overload, ConstructionSignature.EqualityComparer);
                    }
                }

                break;
            case [_]:
                foreach (MethodBase overload in EnumerateOverloads())
                {
                    if (overload.GetParameters() is not [ParameterInfo first, ParameterInfo second])
                    {
                        continue;
                    }

                    ConstructionSignature comparerType = IsAcceptableConstructorPair(first, second, CollectionConstructorParameterType.CollectionOfT);
                    if (comparerType != ConstructionSignature.None)
                    {
                        return (overload, comparerType);
                    }
                }

                break;
        }

        return null;

        IEnumerable<MethodBase> EnumerateOverloads()
        {
            if (nonComparerOverload is ConstructorInfo { DeclaringType: not null })
            {
                foreach (ConstructorInfo ctor in nonComparerOverload.DeclaringType!.GetConstructors())
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
