using PolyType.Abstractions;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace PolyType.ReflectionProvider;

[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
internal static class InternalTypeShapeExtensions
{
    internal static ITypeShape? GetAssociatedTypeShape(this ITypeShape self, Type associatedType)
    {
        if (associatedType.IsGenericTypeDefinition && self.Type.GenericTypeArguments.Length != associatedType.GetTypeInfo().GenericTypeParameters.Length)
        {
            throw new ArgumentException($"Related type arity ({associatedType.GenericTypeArguments.Length}) mismatch with original type ({self.Type.GenericTypeArguments.Length}).");
        }

        Type closedType = associatedType.IsGenericTypeDefinition
            ? associatedType.MakeGenericType(self.Type.GenericTypeArguments)
            : associatedType;

        return self.Provider.GetShape(closedType);
    }

    internal static CollectionComparerOptions ToComparerConstruction(ConstructionWithComparer signature)
        => signature switch
        {
            ConstructionWithComparer.Comparer or ConstructionWithComparer.ComparerValues or ConstructionWithComparer.ValuesComparer => CollectionComparerOptions.Comparer,
            ConstructionWithComparer.EqualityComparer or ConstructionWithComparer.EqualityComparerValues or ConstructionWithComparer.ValuesEqualityComparer => CollectionComparerOptions.EqualityComparer,
            ConstructionWithComparer.None => CollectionComparerOptions.None,
            _ => throw new NotImplementedException(),
        };

    internal static (ConstructionWithComparer, ConstructorInfo?) FindComparerConstructorOverload(this ICollectionShape shape, ConstructorInfo? nonComparerOverload)
    {
        var (comparer, overload) = FindComparerConstructionOverload(shape, nonComparerOverload);
        return (comparer, (ConstructorInfo?)overload);
    }

    internal static (ConstructionWithComparer, MethodBase?) FindComparerConstructionOverload(this ICollectionShape shape, MethodBase? nonComparerOverload)
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

                    switch (ToComparerConstruction(shape, onlyParameter))
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

                    ConstructionWithComparer comparerType = IsAcceptableConstructorPair(shape, first, second, CollectionConstructorParameterType.CollectionOfT);
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

    internal static CollectionComparerOptions ToComparerConstruction(this ICollectionShape shape, ParameterInfo parameter)
        => shape.ClassifyConstructorParameter(parameter) switch
        {
            CollectionConstructorParameterType.IComparerOfT => CollectionComparerOptions.Comparer,
            CollectionConstructorParameterType.IEqualityComparerOfT => CollectionComparerOptions.EqualityComparer,
            _ => CollectionComparerOptions.None,
        };

    internal static ConstructionWithComparer IsAcceptableConstructorPair(CollectionConstructorParameterType first, CollectionConstructorParameterType second, CollectionConstructorParameterType collectionType)
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

    internal static ConstructionWithComparer IsAcceptableConstructorPair(ICollectionShape shape, ParameterInfo first, ParameterInfo second, CollectionConstructorParameterType collectionType)
        => IsAcceptableConstructorPair(shape.ClassifyConstructorParameter(first), shape.ClassifyConstructorParameter(second), collectionType);

    internal static Func<object, SpanConstructor<TElement, TResult>> CreateSpanMethodDelegate<TElement, TCompare, TResult>(MethodInfo methodInfo, ConstructionWithComparer signatureStyle)
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

    internal static object? GetRelevantComparer<TKey>(CollectionConstructionOptions<TKey>? collectionConstructionOptions, CollectionComparerOptions customComparerConstruction)
        => customComparerConstruction switch
        {
            CollectionComparerOptions.Comparer => collectionConstructionOptions?.Comparer,
            CollectionComparerOptions.EqualityComparer => collectionConstructionOptions?.EqualityComparer,
            _ => null,
        };
}
