using PolyType.Abstractions;
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
    SpanConstructor<TElement, TResult> CreateSpanConstructorWithLeadingECDelegate<TElement, TKey, TResult>(ConstructorInfo ctorInfo, IEqualityComparer<TKey> comparer);
    SpanConstructor<TElement, TResult> CreateSpanConstructorWithTrailingECDelegate<TElement, TKey, TResult>(ConstructorInfo ctorInfo, IEqualityComparer<TKey> comparer);
    SpanConstructor<TElement, TResult> CreateSpanConstructorWithLeadingCDelegate<TElement, TKey, TResult>(ConstructorInfo ctorInfo, IComparer<TKey> comparer);
    SpanConstructor<TElement, TResult> CreateSpanConstructorWithTrailingCDelegate<TElement, TKey, TResult>(ConstructorInfo ctorInfo, IComparer<TKey> comparer);

    Getter<TUnion, int> CreateGetUnionCaseIndex<TUnion>(DerivedTypeInfo[] derivedTypeInfos);
}
