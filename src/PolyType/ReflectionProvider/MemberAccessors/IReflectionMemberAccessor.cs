using PolyType.Abstractions;
using System.Reflection;

namespace PolyType.ReflectionProvider.MemberAccessors;

internal interface IReflectionMemberAccessor
{
    Getter<TDeclaringType, TPropertyType> CreateGetter<TDeclaringType, TPropertyType>(MemberInfo memberInfo, MemberInfo[]? parentMembers);
    Setter<TDeclaringType, TPropertyType> CreateSetter<TDeclaringType, TPropertyType>(MemberInfo memberInfo, MemberInfo[]? parentMembers);
    Setter<TDeclaringType?, TEventHandler> CreateEventAccessor<TDeclaringType, TEventHandler>(MethodInfo accessor);

    EnumerableAppender<TEnumerable, TElement> CreateEnumerableAppender<TEnumerable, TElement>(MethodInfo methodInfo);
    DictionaryInserter<TDictionary, TKey, TValue> CreateDictionaryInserter<TDictionary, TKey, TValue>(MutableCollectionConstructorInfo ctorInfo, DictionaryInsertionMode insertionMode);

    Func<TDeclaringType> CreateDefaultConstructor<TDeclaringType>(IMethodShapeInfo ctorInfo);

    Type CreateConstructorArgumentStateType(IMethodShapeInfo ctorInfo);
    Func<TArgumentState> CreateConstructorArgumentStateCtor<TArgumentState>(IMethodShapeInfo ctorInfo)
        where TArgumentState : IArgumentState;

    Getter<TArgumentState, TParameter> CreateArgumentStateGetter<TArgumentState, TParameter>(IMethodShapeInfo ctorInfo, int parameterIndex)
        where TArgumentState : IArgumentState;

    Setter<TArgumentState, TParameter> CreateArgumentStateSetter<TArgumentState, TParameter>(IMethodShapeInfo ctorInfo, int parameterIndex)
        where TArgumentState : IArgumentState;

    Constructor<TArgumentState, TDeclaringType> CreateParameterizedConstructor<TArgumentState, TDeclaringType>(IMethodShapeInfo ctorInfo)
        where TArgumentState : IArgumentState;

    MethodInvoker<TDeclaringType?, TArgumentState, TResult> CreateMethodInvoker<TDeclaringType, TArgumentState, TResult>(MethodShapeInfo methodInfo)
        where TArgumentState : IArgumentState;

    MethodInvoker<TFunction, TArgumentState, TResult> CreateFSharpFunctionInvoker<TFunction, TArgumentState, TResult>(FSharpFuncInfo funcInfo)
        where TArgumentState : IArgumentState;

    Func<RefFunc<TArgumentState, TResult>, TDelegate> CreateDelegateWrapper<TDelegate, TArgumentState, TResult>(MethodShapeInfo shapeInfo)
        where TDelegate : Delegate
        where TArgumentState : IArgumentState;

    bool IsCollectionConstructorSupported(MethodBase method, CollectionConstructorParameter[] signature);
    MutableCollectionConstructor<TKey, TCollection> CreateMutableCollectionConstructor<TKey, TElement, TCollection>(MutableCollectionConstructorInfo constructorInfo);
    ParameterizedCollectionConstructor<TKey, TElement, TCollection> CreateParameterizedCollectionConstructor<TKey, TElement, TCollection>(ParameterizedCollectionConstructorInfo constructorInfo);

    Getter<TUnion, int> CreateGetUnionCaseIndex<TUnion>(DerivedTypeInfo[] derivedTypeInfos);
}
