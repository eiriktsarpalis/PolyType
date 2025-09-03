using PolyType.Abstractions;
using PolyType.SourceGenModel;
using System.Buffers;
using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace PolyType.ReflectionProvider.MemberAccessors;

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class ReflectionMemberAccessor : IReflectionMemberAccessor
{
    public Getter<TDeclaringType, TPropertyType> CreateGetter<TDeclaringType, TPropertyType>(MemberInfo memberInfo, MemberInfo[]? parentMembers)
    {
        Debug.Assert(memberInfo is PropertyInfo or FieldInfo);

        if (parentMembers is null or { Length: 0 })
        {
            if (memberInfo is PropertyInfo p)
            {
                if (p.DeclaringType!.IsValueType)
                {
                    // If a struct we can wrap the getter in the getter delegate directly.
                    return p.GetMethod!.CreateDelegate<Getter<TDeclaringType, TPropertyType>>();
                }

                // Reference types can't be wrapped directly, so we create an intermediate func delegate.
                Func<TDeclaringType, TPropertyType> getterDelegate = p.GetMethod!.CreateDelegate<Func<TDeclaringType, TPropertyType>>();
                return (ref TDeclaringType obj) => getterDelegate(obj);
            }

            // https://github.com/mono/mono/issues/10372
            var f = (FieldInfo)memberInfo;
            return ReflectionHelpers.IsMonoRuntime
                ? (ref TDeclaringType obj) => (TPropertyType)f.GetValue(obj)!
                : (ref TDeclaringType obj) => (TPropertyType)f.GetValueDirect(__makeref(obj))!;
        }

        Debug.Assert(typeof(TDeclaringType).IsNestedTupleRepresentation());

        if (typeof(TDeclaringType).IsValueType)
        {
            Debug.Assert(memberInfo is FieldInfo);
            Debug.Assert(parentMembers is FieldInfo[]);

            var fieldInfo = (FieldInfo)memberInfo;
            var parentFields = (FieldInfo[])parentMembers;
            return (ref TDeclaringType obj) =>
            {
                object boxedObj = obj!;
                for (int i = 0; i < parentFields.Length; i++)
                {
                    boxedObj = parentFields[i].GetValue(boxedObj)!;
                }

                return (TPropertyType)fieldInfo.GetValue(boxedObj)!;
            };
        }
        else
        {
            Debug.Assert(memberInfo is PropertyInfo);
            Debug.Assert(parentMembers is PropertyInfo[]);

            var propertyInfo = (PropertyInfo)memberInfo;
            var parentProperties = (PropertyInfo[])parentMembers;
            return (ref TDeclaringType obj) =>
            {
                object boxedObj = obj!;
                for (int i = 0; i < parentProperties.Length; i++)
                {
                    boxedObj = parentProperties[i].GetValue(boxedObj)!;
                }

                return (TPropertyType)propertyInfo.GetValue(boxedObj)!;
            };
        }
    }

    public Setter<TDeclaringType, TPropertyType> CreateSetter<TDeclaringType, TPropertyType>(MemberInfo memberInfo, MemberInfo[]? parentMembers)
    {
        Debug.Assert(memberInfo is PropertyInfo or FieldInfo);

        if (parentMembers is null or { Length: 0 })
        {
            if (memberInfo is PropertyInfo p)
            {
                MethodInfo setter = p.SetMethod!;
                if (p.DeclaringType!.IsValueType)
                {
                    // If a struct we can wrap the setter in the setter delegate directly.
                    return setter.CreateDelegate<Setter<TDeclaringType, TPropertyType>>();
                }

                // Reference types can't be wrapped directly, so we create an intermediate action delegate.
                Action<TDeclaringType, TPropertyType> setterDelegate = setter.CreateDelegate<Action<TDeclaringType, TPropertyType>>();
                return (ref TDeclaringType obj, TPropertyType value) => setterDelegate(obj, value);
            }

            // https://github.com/mono/mono/issues/10372
            var f = (FieldInfo)memberInfo;
            return ReflectionHelpers.IsMonoRuntime
                ? (ref TDeclaringType obj, TPropertyType value) =>
                {
                    object boxedObj = obj!;
                    f.SetValue(boxedObj, value);
                    obj = (TDeclaringType)boxedObj;
                }
            : (ref TDeclaringType obj, TPropertyType value) => f.SetValueDirect(__makeref(obj), value!);
        }

        Debug.Assert(typeof(TDeclaringType).IsNestedTupleRepresentation());
        Debug.Assert(typeof(TDeclaringType).IsValueTupleType(), "only value tuples are mutable.");
        Debug.Assert(memberInfo is FieldInfo);
        Debug.Assert(parentMembers is FieldInfo[]);

        var fieldInfo = (FieldInfo)memberInfo;
        var parentFields = (FieldInfo[])parentMembers;
        return (ref TDeclaringType obj, TPropertyType value) =>
        {
            object?[] boxedValues = new object[parentFields.Length + 1];
            boxedValues[0] = obj;

            for (int i = 0; i < parentFields.Length; i++)
            {
                boxedValues[i + 1] = parentFields[i].GetValue(boxedValues[i]);
            }

            fieldInfo.SetValue(boxedValues[^1], value);

            for (int i = parentFields.Length - 1; i >= 0; i--)
            {
                parentFields[i].SetValue(boxedValues[i], boxedValues[i + 1]);
            }

            obj = (TDeclaringType)boxedValues[0]!;
        };
    }

    public Setter<TDeclaringType?, TEventHandler> CreateEventAccessor<TDeclaringType, TEventHandler>(MethodInfo accessor)
    {
        return !typeof(TDeclaringType).IsValueType
            ? (ref TDeclaringType? target, TEventHandler handler) => accessor.Invoke(target, [handler])
            : (ref TDeclaringType? target, TEventHandler handler) =>
            {
                object? boxedTarget = target;
                accessor.Invoke(boxedTarget, [handler]);
                target = (TDeclaringType?)boxedTarget;
            };
    }

    public EnumerableAppender<TEnumerable, TElement> CreateEnumerableAppender<TEnumerable, TElement>(MethodInfo addMethod)
    {
        return !typeof(TEnumerable).IsValueType
        ? (ref TEnumerable enumerable, TElement element) => addMethod.Invoke(enumerable, [element]) is not bool success || success
        : (ref TEnumerable enumerable, TElement element) =>
        {
            object boxed = enumerable!;
            bool success = addMethod.Invoke(boxed, [element]) is not bool s || s;
            enumerable = (TEnumerable)boxed;
            return success;
        };
    }

    public DictionaryInserter<TDictionary, TKey, TValue> CreateDictionaryInserter<TDictionary, TKey, TValue>(MutableCollectionConstructorInfo ctorInfo, DictionaryInsertionMode insertionMode)
    {
        switch (insertionMode)
        {
            case DictionaryInsertionMode.Overwrite or DictionaryInsertionMode.Throw:
                DebugExt.Assert(ctorInfo.SetMethod is not null || ctorInfo.AddMethod is not null);
                var insertMethod = insertionMode is DictionaryInsertionMode.Overwrite ? ctorInfo.SetMethod! : ctorInfo.AddMethod!;
                if (typeof(TDictionary).IsValueType)
                {
                    var setDelegate = insertMethod.CreateDelegate<RefAction<TDictionary, TKey, TValue>>();
                    return (ref TDictionary dict, TKey key, TValue value) =>
                    {
                        setDelegate(ref dict, key, value);
                        return true;
                    };
                }
                else
                {
                    var setDelegate = insertMethod.CreateDelegate<Action<TDictionary, TKey, TValue>>();
                    return (ref TDictionary dict, TKey key, TValue value) =>
                    {
                        setDelegate(dict, key, value);
                        return true; // Always returns true since it overwrites existing keys.
                    };
                }

            case DictionaryInsertionMode.Discard when ctorInfo.TryAddMethod is not null:
                if (typeof(TDictionary).IsValueType)
                {
                    return ctorInfo.TryAddMethod.CreateDelegate<DictionaryInserter<TDictionary, TKey, TValue>>();
                }
                else
                {
                    var tryAddDelegate = ctorInfo.TryAddMethod.CreateDelegate<Func<TDictionary, TKey, TValue, bool>>();
                    return (ref TDictionary dict, TKey key, TValue value) => tryAddDelegate(dict, key, value);
                }

            case DictionaryInsertionMode.Discard:
                DebugExt.Assert(ctorInfo.ContainsKeyMethod is not null && ctorInfo is { AddMethod: not null } or { SetMethod: not null });
                var addMethod = ctorInfo.AddMethod ?? ctorInfo.SetMethod;
                if (typeof(TDictionary).IsValueType)
                {
                    var containsKeyDelegate = ctorInfo.ContainsKeyMethod.CreateDelegate<RefFunc<TDictionary, TKey, bool>>();
                    var addMethodDelegate = addMethod!.CreateDelegate<RefAction<TDictionary, TKey, TValue>>();
                    return (ref TDictionary dict, TKey key, TValue value) =>
                    {
                        if (containsKeyDelegate(ref dict, key))
                        {
                            return false; // Key already exists, discard the insertion.
                        }

                        addMethodDelegate(ref dict, key, value);
                        return true; // Successfully added the new key/value pair.
                    };
                }
                else
                {
                    var containsKeyDelegate = ctorInfo.ContainsKeyMethod.CreateDelegate<Func<TDictionary, TKey, bool>>();
                    var addMethodDelegate = addMethod!.CreateDelegate<Action<TDictionary, TKey, TValue>>();
                    return (ref TDictionary dict, TKey key, TValue value) =>
                    {
                        if (containsKeyDelegate(dict, key))
                        {
                            return false; // Key already exists, discard the insertion.
                        }

                        addMethodDelegate(dict, key, value);
                        return true; // Successfully added the new key/value pair.
                    };
                }

            default:
                throw new NotSupportedException();
        }
    }

    public Func<TDeclaringType> CreateDefaultConstructor<TDeclaringType>(IMethodShapeInfo ctorInfo)
    {
        Debug.Assert(ctorInfo.Parameters is []);
        Debug.Assert(ctorInfo is MethodShapeInfo);
        return ((MethodShapeInfo)ctorInfo).Method is { } cI
            ? () => (TDeclaringType)cI.Invoke(null)!
            : static () => default(TDeclaringType)!;
    }

    public Type CreateConstructorArgumentStateType(IMethodShapeInfo ctorInfo)
    {
        return ctorInfo switch
        {
            { Parameters: [] } => typeof(EmptyArgumentState),
            MethodShapeInfo { MemberInitializers.Length: > 0 } => typeof(LargeArgumentState<(object?[], object?[])>),
            _ => typeof(LargeArgumentState<object?[]>),
        };
    }

    public Func<TArgumentState> CreateConstructorArgumentStateCtor<TArgumentState>(IMethodShapeInfo ctorInfo)
        where TArgumentState : IArgumentState
    {
        if (ctorInfo.Parameters is [])
        {
            Debug.Assert(typeof(TArgumentState) == typeof(EmptyArgumentState));
            return (Func<TArgumentState>)(object)(static () => EmptyArgumentState.Instance);
        }

        if (ctorInfo is MethodShapeInfo { MemberInitializers.Length: > 0 } ctor)
        {
            Debug.Assert(typeof(TArgumentState) == typeof(LargeArgumentState<(object?[], object?[])>));
            Func<object?[]> createCtorParameterArray = CreateConstructorArgumentArrayFunc(ctor.ConstructorParameters);
            int memberInitializerLength = ctor.MemberInitializers.Length;
            ValueBitArray requiredPropertiesMask = CreateRequiredParametersMask(ctorInfo);
            return (Func<TArgumentState>)(object)new Func<LargeArgumentState<(object?[], object?[])>>(
                () => new((createCtorParameterArray(), new object?[memberInitializerLength]), ctor.Parameters.Length, requiredPropertiesMask));
        }
        else
        {
            Debug.Assert(typeof(TArgumentState) == typeof(LargeArgumentState<object?[]>));
            Func<object?[]> createCtorParameterArray = CreateConstructorArgumentArrayFunc(ctorInfo.Parameters);
            ValueBitArray requiredPropertiesMask = CreateRequiredParametersMask(ctorInfo);
            return (Func<TArgumentState>)(object)new Func<LargeArgumentState<object?[]>>(
                () => new(createCtorParameterArray(), ctorInfo.Parameters.Length, requiredPropertiesMask));
        }

        static ValueBitArray CreateRequiredParametersMask(IMethodShapeInfo ctorInfo)
        {
            ValueBitArray mask = new(ctorInfo.Parameters.Length);
            for (int i = 0; i < ctorInfo.Parameters.Length; i++)
            {
                if (ctorInfo.Parameters[i].IsRequired)
                {
                    mask[i] = true;
                }
            }

            return mask;
        }

        static Func<object?[]> CreateConstructorArgumentArrayFunc(IParameterShapeInfo[] parameters)
        {
            int arity = parameters.Length;
            if (arity == 0)
            {
                return static () => [];
            }
            else if (parameters.Any(param => param.HasDefaultValue))
            {
                object?[] sourceParamArray = parameters.Select(p => p.DefaultValue).ToArray();
                return () => (object?[])sourceParamArray.Clone();
            }
            else
            {
                return () => new object?[arity];
            }
        }
    }

    public Getter<TArgumentState, TParameter> CreateArgumentStateGetter<TArgumentState, TParameter>(IMethodShapeInfo ctorInfo, int parameterIndex)
        where TArgumentState : IArgumentState
    {
        Debug.Assert(ctorInfo.Parameters.Length > 0);
        if (ctorInfo is MethodShapeInfo { MemberInitializers.Length: > 0 } ctor)
        {
            Debug.Assert(typeof(TArgumentState) == typeof(LargeArgumentState<(object?[], object?[])>));
            int initializerIndex = parameterIndex - ctor.ConstructorParameters.Length;
            return (Getter<TArgumentState, TParameter>)(object)new Getter<LargeArgumentState<(object?[], object?[])>, TParameter>(
                (ref LargeArgumentState<(object?[] ctorArgs, object?[] memberArgs)> state) =>
                {
                    return Cast(initializerIndex < 0 ? state.Arguments.ctorArgs[parameterIndex] : state.Arguments.memberArgs[initializerIndex]);
                });
        }
        else
        {
            Debug.Assert(typeof(TArgumentState) == typeof(LargeArgumentState<object?[]>));
            return (Getter<TArgumentState, TParameter>)(object)new Getter<LargeArgumentState<object?[]>, TParameter>(
                (ref LargeArgumentState<object?[]> state) => Cast(state.Arguments[parameterIndex]));
        }

        static TParameter Cast(object? value) => typeof(TParameter).IsValueType && value is null ? default! : (TParameter)value!;
    }

    public Setter<TArgumentState, TParameter> CreateArgumentStateSetter<TArgumentState, TParameter>(IMethodShapeInfo ctorInfo, int parameterIndex)
        where TArgumentState : IArgumentState
    {
        Debug.Assert(ctorInfo.Parameters.Length > 0);
        if (ctorInfo is MethodShapeInfo { MemberInitializers.Length: > 0 } ctor)
        {
            Debug.Assert(typeof(TArgumentState) == typeof(LargeArgumentState<(object?[], object?[])>));
            int initializerIndex = parameterIndex - ctor.ConstructorParameters.Length;
            return (Setter<TArgumentState, TParameter>)(object)new Setter<LargeArgumentState<(object?[], object?[])>, TParameter>(
                (ref LargeArgumentState<(object?[] ctorArgs, object?[] memberArgs)> state, TParameter value) =>
                {
                    if (initializerIndex < 0)
                    {
                        state.Arguments.ctorArgs[parameterIndex] = value;
                    }
                    else
                    {
                        state.Arguments.memberArgs[initializerIndex] = value;
                    }

                    state.MarkArgumentSet(parameterIndex);
                });
        }
        else
        {
            Debug.Assert(typeof(TArgumentState) == typeof(LargeArgumentState<object?[]>));
            return (Setter<TArgumentState, TParameter>)(object)new Setter<LargeArgumentState<object?[]>, TParameter>(
                (ref LargeArgumentState<object?[]> state, TParameter value) =>
                {
                    state.Arguments[parameterIndex] = value;
                    state.MarkArgumentSet(parameterIndex);
                });
        }
    }

    public Constructor<TArgumentState, TDeclaringType> CreateParameterizedConstructor<TArgumentState, TDeclaringType>(IMethodShapeInfo ctorInfo)
        where TArgumentState : IArgumentState
    {
        Debug.Assert(ctorInfo.Parameters.Length > 0);
        if (ctorInfo is TupleConstructorShapeInfo tupleCtor)
        {
            Debug.Assert(typeof(TArgumentState) == typeof(LargeArgumentState<object?[]>));
            List<(ConstructorInfo, int)> ctorStack = new();
            for (TupleConstructorShapeInfo? current = tupleCtor; current != null; current = current.NestedTupleConstructor)
            {
                ctorStack.Add((current.ConstructorInfo, current.ConstructorParameters.Length));
            }

            ctorStack.Reverse();

            return (Constructor<TArgumentState, TDeclaringType>)(object)new Constructor<LargeArgumentState<object?[]>, TDeclaringType>(
                (ref LargeArgumentState<object?[]> state) =>
            {
                object?[] arguments = state.Arguments;
                object? result = null;
                int i = arguments.Length;
                foreach ((ConstructorInfo ctorInfo, int arity) in ctorStack)
                {
                    object?[] localParams;
                    if (i == arguments.Length)
                    {
#if NET
                        localParams = arguments[^arity..];
#else
                        // https://github.com/Sergio0694/PolySharp/issues/104
                        localParams = new object?[arity];
                        arguments.AsSpan(arguments.Length - arity).CopyTo(localParams);
#endif
                    }
                    else
                    {
                        localParams = new object?[arity + 1];
                        arguments.AsSpan(i - arity, arity).CopyTo(localParams);
                        localParams[arity] = result;
                    }

                    result = ctorInfo.Invoke(localParams);
                    i -= arity;
                }

                return (TDeclaringType)result!;
            });
        }

        if (ctorInfo is MethodShapeInfo { MemberInitializers.Length: > 0 } methodCtor)
        {
            Debug.Assert(typeof(TArgumentState) == typeof(LargeArgumentState<(object?[], object?[])>));
            MemberInitializerShapeInfo[] memberInitializers = methodCtor.MemberInitializers;
            if (methodCtor.Method is { } ctor)
            {
                int ctorArity = methodCtor.ConstructorParameters.Length;
                return (Constructor<TArgumentState, TDeclaringType>)(object)new Constructor<LargeArgumentState<(object?[], object?[])>, TDeclaringType>(
                    (ref LargeArgumentState<(object?[] ctorArgs, object?[] memberArgs)> state) =>
                    {
                        object obj = ctor.Invoke(state.Arguments.ctorArgs)!;
                        PopulateMemberInitializers(ref state, obj, ctorArity, memberInitializers, state.Arguments.memberArgs);
                        return (TDeclaringType)obj!;
                    });
            }
            else
            {
                return (Constructor<TArgumentState, TDeclaringType>)(object)new Constructor<LargeArgumentState<(object?[], object?[])>, TDeclaringType>(
                    (ref LargeArgumentState<(object?[] ctorArgs, object?[] memberArgs)> state) =>
                    {
                        object obj = default(TDeclaringType)!;
                        PopulateMemberInitializers(ref state, obj, ctorArity: 0, memberInitializers, state.Arguments.memberArgs);
                        return (TDeclaringType)obj!;
                    });
            }

            static void PopulateMemberInitializers<TArgState>(ref TArgState state, object obj, int ctorArity, MemberInitializerShapeInfo[] memberInitializers, object?[] memberArgs)
                where TArgState : IArgumentState
            {
                for (int i = 0; i < memberInitializers.Length; i++)
                {
                    if (!state.IsArgumentSet(ctorArity + i))
                    {
                        continue; // Skip to avoid setting uninitialized members.
                    }

                    MemberInfo member = memberInitializers[i].MemberInfo;

                    if (member is PropertyInfo prop)
                    {
                        prop.SetValue(obj, memberArgs[i]);
                    }
                    else
                    {
                        ((FieldInfo)member).SetValue(obj, memberArgs[i]);
                    }
                }
            }
        }

        Debug.Assert(ctorInfo is MethodShapeInfo { Method: not null });
        Debug.Assert(typeof(TArgumentState) == typeof(LargeArgumentState<object?[]>));
        var cI = ((MethodShapeInfo)ctorInfo).Method!;
        return (Constructor<TArgumentState, TDeclaringType>)(object)new Constructor<LargeArgumentState<object?[]>, TDeclaringType>(
            (ref LargeArgumentState<object?[]> state) => (TDeclaringType)cI.Invoke(state.Arguments)!);
    }

    public MethodInvoker<TDeclaringType?, TArgumentState, TResult> CreateMethodInvoker<TDeclaringType, TArgumentState, TResult>(MethodShapeInfo ctorInfo) where TArgumentState : IArgumentState
    {
        DebugExt.Assert(ctorInfo.Method is MethodInfo);
        var methodInfo = (MethodInfo)ctorInfo.Method;

        Func<object?, ValueTask<TResult>> returnMarshaler = CreateResultMarshaler(methodInfo.ReturnType);
        if (ctorInfo.Parameters is [])
        {
            Debug.Assert(typeof(TArgumentState) == typeof(EmptyArgumentState));
            return (ref TDeclaringType? target, ref TArgumentState _) =>
            {
                object? boxedTarget = target;
                object? result = methodInfo.Invoke(boxedTarget, []);
                target = (TDeclaringType?)boxedTarget;
                return returnMarshaler(result);
            };
        }

        Debug.Assert(typeof(TArgumentState) == typeof(LargeArgumentState<object?[]>));
        return (MethodInvoker<TDeclaringType?, TArgumentState, TResult>)(object)new MethodInvoker<TDeclaringType?, LargeArgumentState<object?[]>, TResult>(
            (ref TDeclaringType? target, ref LargeArgumentState<object?[]> state) =>
            {
                object? boxedTarget = target;
                object? result = methodInfo.Invoke(boxedTarget, state.Arguments);
                target = (TDeclaringType?)boxedTarget;
                return returnMarshaler(result);
            });

        static Func<object?, ValueTask<TResult>> CreateResultMarshaler(Type methodReturnType)
        {
            if (methodReturnType == typeof(void))
            {
                Debug.Assert(typeof(TResult) == typeof(Unit));
                return (Func<object?, ValueTask<TResult>>)(object)(static (object? _) => new ValueTask<Unit>(Unit.Value));
            }

            if (methodReturnType == typeof(Task))
            {
                Debug.Assert(typeof(TResult) == typeof(Unit));
                return (Func<object?, ValueTask<TResult>>)(object)new Func<object?, ValueTask<Unit>>(static value => Unit.FromTaskAsync((Task)value!));
            }

            if (methodReturnType == typeof(ValueTask))
            {
                Debug.Assert(typeof(TResult) == typeof(Unit));
                return (Func<object?, ValueTask<TResult>>)(object)new Func<object?, ValueTask<Unit>>(static (object? value) => Unit.FromValueTaskAsync((ValueTask)value!));
            }

            if (methodReturnType == typeof(ValueTask<TResult>))
            {
                return static value => (ValueTask<TResult>)value!;
            }

            if (methodReturnType == typeof(Task<TResult>))
            {
                return static value => new ValueTask<TResult>((Task<TResult>)value!);
            }

            Debug.Assert(methodReturnType == typeof(TResult) || (methodReturnType.IsByRef && methodReturnType.GetElementType() == typeof(TResult)));
            return static value => new ValueTask<TResult>((TResult)value!);
        }
    }

    public Func<RefFunc<TArgumentState, TResult>, TDelegate> CreateDelegateWrapper<TDelegate, TArgumentState, TResult>(MethodShapeInfo shapeInfo)
        where TDelegate : Delegate
        where TArgumentState : IArgumentState
    {
        throw new NotSupportedException("Delegate creation is not supported when reflection emit is disabled.");
    }

    public Getter<TUnion, int> CreateGetUnionCaseIndex<TUnion>(DerivedTypeInfo[] derivedTypeInfos)
    {
        Debug.Assert(!typeof(TUnion).IsValueType);
        Debug.Assert(derivedTypeInfos.Length > 0);

        ConcurrentDictionary<Type, int> cache = new();
        int defaultIndex = -1;
        foreach (DerivedTypeInfo derivedTypeInfo in derivedTypeInfos)
        {
            cache.TryAdd(derivedTypeInfo.Type, derivedTypeInfo.Index);
            if (derivedTypeInfo.Type == typeof(TUnion))
            {
                defaultIndex = derivedTypeInfo.Index;
            }
        }

        // Add the base type as a sentinel value if it hasn't been added by the attributes yet.
        cache.TryAdd(typeof(TUnion), defaultIndex);

        return (ref TUnion union) =>
        {
            if (union is null)
            {
                return defaultIndex;
            }

            Type unionType = union.GetType();
            if (cache.TryGetValue(unionType, out int index))
            {
                return index;
            }

            return ComputeIndexForType(unionType);
        };

        int ComputeIndexForType(Type type)
        {
            int foundIndex = defaultIndex;
            foreach (Type parentType in CommonHelpers.TraverseGraphWithTopologicalSort(type, GetParentTypes))
            {
                if (cache.TryGetValue(parentType, out int i))
                {
                    foundIndex = i;
                    break;
                }
            }

            cache[type] = foundIndex; // Cache for future use.
            return foundIndex;

            static List<Type> GetParentTypes(Type type)
            {
                Debug.Assert(typeof(TUnion).IsAssignableFrom(type));
                List<Type> parentTypes = [];

                if (type == typeof(TUnion))
                {
                    return parentTypes;
                }

                if (typeof(TUnion).IsAssignableFrom(type.BaseType))
                {
                    parentTypes.Add(type.BaseType);
                }

                if (typeof(TUnion).IsInterface)
                {
                    foreach (Type interfaceType in type.GetInterfaces())
                    {
                        if (typeof(TUnion).IsAssignableFrom(interfaceType))
                        {
                            parentTypes.Add(interfaceType);
                        }
                    }
                }

                return parentTypes;
            }
        }
    }

    public bool IsCollectionConstructorSupported(MethodBase method, CollectionConstructorParameter[] signature)
    {
        if (signature.Contains(CollectionConstructorParameter.Span))
        {
            return method is MethodInfo &&
                signature is [CollectionConstructorParameter.Span]
                          or [CollectionConstructorParameter.Span, CollectionConstructorParameter.EqualityComparerOptional];
        }

        return true;
    }

    public MutableCollectionConstructor<TKey, TDeclaringType> CreateMutableCollectionConstructor<TKey, TElement, TDeclaringType>(MutableCollectionConstructorInfo collectionCtorInfo)
    {
        SpanAction<TElement, CollectionConstructionOptions<TKey>, object?[]>[] argumentSetters = collectionCtorInfo.Signature
            .Select(CreateArgumentSetter<TElement, TKey>)
            .ToArray();

        var ctorInfo = collectionCtorInfo.Factory;
        return (in CollectionConstructionOptions<TKey> opts) =>
        {
            var args = new object?[argumentSetters.Length];
            for (int i = 0; i < argumentSetters.Length; i++)
            {
                argumentSetters[i]([], opts, args);
            }

            return (TDeclaringType)ctorInfo.Invoke(args)!;
        };
    }

    public ParameterizedCollectionConstructor<TKey, TElement, TCollection> CreateParameterizedCollectionConstructor<TKey, TElement, TCollection>(ParameterizedCollectionConstructorInfo constructorInfo)
    {
        if (constructorInfo is { Factory: MethodInfo methodInfo, Signature: [CollectionConstructorParameter.Span] })
        {
            SpanFunc<TElement, TCollection> spanFunc = methodInfo.CreateDelegate<SpanFunc<TElement, TCollection>>();
            return (ReadOnlySpan<TElement> span, in CollectionConstructionOptions<TKey> _) => spanFunc(span);
        }

        if (constructorInfo is { Factory: MethodInfo methodInfo2, Signature: [CollectionConstructorParameter.Span, CollectionConstructorParameter.EqualityComparerOptional] })
        {
            SpanFunc<TElement, IEqualityComparer<TKey>?, TCollection> spanFunc = methodInfo2.CreateDelegate<SpanFunc<TElement, IEqualityComparer<TKey>?, TCollection>>();
            return (ReadOnlySpan<TElement> span, in CollectionConstructionOptions<TKey> options) => spanFunc(span, options.EqualityComparer);
        }

        SpanAction<TElement, CollectionConstructionOptions<TKey>, object?[]>[] argumentSetters = constructorInfo.Signature
            .Select(CreateArgumentSetter<TElement, TKey>)
            .ToArray();

        MethodBase factory = constructorInfo.Factory;
        return (ReadOnlySpan<TElement> span, in CollectionConstructionOptions<TKey> opts) =>
        {
            var args = new object?[argumentSetters.Length];
            for (int i = 0; i < argumentSetters.Length; i++)
            {
                argumentSetters[i](span, opts, args);
            }

            return (TCollection)factory.Invoke(args)!;
        };
    }

    private static SpanAction<TElement, CollectionConstructionOptions<TKey>, object?[]> CreateArgumentSetter<TElement, TKey>(CollectionConstructorParameter type, int index)
    {
        SpanFunc<TElement, IEqualityComparer<TKey>?, IDictionary>? dictionaryFactory = null;
        SpanFunc<TElement, IEnumerable>? tupleEnumerableFactory = null;
        return type switch
        {
            CollectionConstructorParameter.List => (span, opts, args) => args[index] = CollectionHelpers.CreateList(span),
            CollectionConstructorParameter.HashSet => (span, opts, args) => args[index] = CollectionHelpers.CreateHashSet(span, (IEqualityComparer<TElement>?)(object?)opts.Capacity),
            CollectionConstructorParameter.Dictionary => (span, opts, args) => args[index] = (dictionaryFactory ??= CreateDictionaryFactory())(span, opts.EqualityComparer),
            CollectionConstructorParameter.TupleEnumerable => (span, opts, args) => args[index] = (tupleEnumerableFactory ??= CreateTupleEnumerableFactory())(span),
            CollectionConstructorParameter.Capacity => (span, opts, args) => args[index] = opts.Capacity ?? 0,
            CollectionConstructorParameter.CapacityOptional => (span, opts, args) => args[index] = opts.Capacity,
            CollectionConstructorParameter.EqualityComparer => (span, opts, args) => args[index] = opts.EqualityComparer ?? EqualityComparer<TKey>.Default,
            CollectionConstructorParameter.EqualityComparerOptional => (span, opts, args) => args[index] = opts.EqualityComparer,
            CollectionConstructorParameter.Comparer => (span, opts, args) => args[index] = opts.Comparer ?? Comparer<TKey>.Default,
            CollectionConstructorParameter.ComparerOptional => (span, opts, args) => args[index] = opts.Comparer,
            CollectionConstructorParameter.Span => (span, opts, args) => throw new NotSupportedException("Constructors accepting span parameters are not supported when reflection emit is disabled."),
            _ => throw new NotSupportedException(type.ToString()), // Not supported in the current implementation.
        };

        SpanFunc<TElement, IEqualityComparer<TKey>?, IDictionary> CreateDictionaryFactory()
        {
            Debug.Assert(typeof(TElement).IsGenericType && typeof(TElement).GetGenericTypeDefinition() == typeof(KeyValuePair<,>));
            MethodInfo factory = typeof(CollectionHelpers)
                .GetMethod(nameof(CollectionHelpers.CreateDictionary), BindingFlags.Public | BindingFlags.Static)!
                .MakeGenericMethod(typeof(TElement).GetGenericArguments());

            return factory.CreateDelegate<SpanFunc<TElement, IEqualityComparer<TKey>?, IDictionary>>();
        }

        SpanFunc<TElement, IEnumerable> CreateTupleEnumerableFactory()
        {
            Debug.Assert(typeof(TElement).IsGenericType && typeof(TElement).GetGenericTypeDefinition() == typeof(KeyValuePair<,>));
            MethodInfo factory = typeof(CollectionHelpers)
                .GetMethod(nameof(CollectionHelpers.CreateTupleArray), BindingFlags.Public | BindingFlags.Static)!
                .MakeGenericMethod(typeof(TElement).GetGenericArguments());

            return factory.CreateDelegate<SpanFunc<TElement, IEnumerable>>();
        }
    }

    private delegate TResult SpanFunc<TElement, TResult>(ReadOnlySpan<TElement> span);
    private delegate TResult SpanFunc<TElement, TArg1, TResult>(ReadOnlySpan<TElement> span, TArg1 arg1);
    private delegate void SpanAction<TElement, TArg1, TArg2>(ReadOnlySpan<TElement> span, TArg1 arg1, TArg2 arg2);
    private delegate void RefAction<T1, T2, T3>(ref T1 arg1, T2 arg2, T3 arg3);
    private delegate TResult RefFunc<T1, T2, TResult>(ref T1 arg1, T2 arg2);
}
