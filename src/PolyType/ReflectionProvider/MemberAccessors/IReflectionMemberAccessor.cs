﻿using PolyType.Abstractions;
using System.Reflection;

namespace PolyType.ReflectionProvider.MemberAccessors;

internal interface IReflectionMemberAccessor
{
    Getter<TDeclaringType, TPropertyType> CreateGetter<TDeclaringType, TPropertyType>(MemberInfo memberInfo, MemberInfo[]? parentMembers);
    Setter<TDeclaringType, TPropertyType> CreateSetter<TDeclaringType, TPropertyType>(MemberInfo memberInfo, MemberInfo[]? parentMembers);

    Setter<TEnumerable, TElement> CreateEnumerableAddDelegate<TEnumerable, TElement>(MethodInfo methodInfo);
    Setter<TDictionary, KeyValuePair<TKey, TValue>> CreateDictionaryAddDelegate<TDictionary, TKey, TValue>(MethodInfo methodInfo);

    Func<TDeclaringType> CreateDefaultConstructor<TDeclaringType>(IConstructorShapeInfo ctorInfo);

    Type CreateConstructorArgumentStateType(IConstructorShapeInfo ctorInfo);
    Func<TArgumentState> CreateConstructorArgumentStateCtor<TArgumentState>(IConstructorShapeInfo ctorInfo);
    Setter<TArgumentState, TParameter> CreateConstructorArgumentStateSetter<TArgumentState, TParameter>(IConstructorShapeInfo ctorInfo, int parameterIndex);
    Constructor<TArgumentState, TDeclaringType> CreateParameterizedConstructor<TArgumentState, TDeclaringType>(IConstructorShapeInfo ctorInfo);

    Func<T, TResult> CreateFuncDelegate<T, TResult>(ConstructorInfo ctorInfo);
    Func<T1, T2, TResult> CreateFuncDelegate<T1, T2, TResult>(ConstructorInfo ctorInfo);
    SpanConstructor<T, TResult> CreateSpanConstructorDelegate<T, TResult>(ConstructorInfo ctorInfo);

    /// <summary>
    /// Creates a delegate that takes a comparer and returns a keyed collection constructor delegate.
    /// </summary>
    /// <typeparam name="TElement">The type of element stored in the collection.</typeparam>
    /// <typeparam name="TCompare">The type of the key in the collection.</typeparam>
    /// <typeparam name="TResult">The type of collection.</typeparam>
    /// <param name="ctorInfo">The collection constructor that takes two parameters.</param>
    /// <param name="signatureStyle">The signature of the constructor, indicating which type of comparer is taken and the parameter order.</param>
    /// <returns>
    /// The delegate that constructs delegates. The argument to this delegate should be an instance of <see cref="IEqualityComparer{T}"/> or <see cref="IComparer{T}"/>,
    /// in accordance with <paramref name="signatureStyle"/>.
    /// </returns>
    Func<object, SpanConstructor<TElement, TResult>> CreateSpanConstructorDelegate<TElement, TCompare, TResult>(ConstructorInfo ctorInfo, ConstructionWithComparer signatureStyle);

    Getter<TUnion, int> CreateGetUnionCaseIndex<TUnion>(DerivedTypeInfo[] derivedTypeInfos);
}
