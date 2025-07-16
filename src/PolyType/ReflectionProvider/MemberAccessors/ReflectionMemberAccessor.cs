using PolyType.Abstractions;
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

    public Setter<TEnumerable, TElement> CreateEnumerableAddDelegate<TEnumerable, TElement>(MethodInfo addMethod)
    {
        return !typeof(TEnumerable).IsValueType
        ? (ref TEnumerable enumerable, TElement element) => addMethod.Invoke(enumerable, new object?[] { element })
        : (ref TEnumerable enumerable, TElement element) =>
        {
            object boxed = enumerable!;
            addMethod.Invoke(boxed, new object?[] { element });
            enumerable = (TEnumerable)boxed;
        };
    }

    public Setter<TDictionary, KeyValuePair<TKey, TValue>> CreateDictionaryAddDelegate<TDictionary, TKey, TValue>(MethodInfo addMethod)
    {
        return !typeof(TDictionary).IsValueType
        ? (ref TDictionary dict, KeyValuePair<TKey, TValue> entry) => addMethod.Invoke(dict, new object?[] { entry.Key, entry.Value })
        : (ref TDictionary dict, KeyValuePair<TKey, TValue> entry) =>
        {
            object boxed = dict!;
            addMethod.Invoke(boxed, new object?[] { entry.Key, entry.Value });
            dict = (TDictionary)boxed;
        };
    }

    public Func<TDeclaringType> CreateDefaultConstructor<TDeclaringType>(IConstructorShapeInfo ctorInfo)
    {
        Debug.Assert(ctorInfo.Parameters is []);
        Debug.Assert(ctorInfo is MethodConstructorShapeInfo);
        return ((MethodConstructorShapeInfo)ctorInfo).ConstructorMethod is { } cI
            ? () => (TDeclaringType)cI.Invoke(null)!
            : static () => default(TDeclaringType)!;
    }

    public Type CreateConstructorArgumentStateType(IConstructorShapeInfo ctorInfo)
    {
        return ctorInfo switch
        {
            { Parameters: [] } => typeof(object),
            { Parameters: [MethodParameterShapeInfo param] } => param.Type,
            MethodConstructorShapeInfo { MemberInitializers.Length: > 0 } => typeof((object?[] ctorArgs, object[]? memberInitializerArgs, BitArray memberInitializerFlags)),
            _ => typeof(object?[]),
        };
    }

    public Func<TArgumentState> CreateConstructorArgumentStateCtor<TArgumentState>(IConstructorShapeInfo ctorInfo)
    {
        Debug.Assert(ctorInfo.Parameters.Length > 0);

        if (ctorInfo.Parameters is [MethodParameterShapeInfo parameter])
        {
            Debug.Assert(typeof(TArgumentState) == parameter.Type);
            TArgumentState? defaultValue = parameter.HasDefaultValue
                ? (TArgumentState?)parameter.DefaultValue
                : default;

            return () => defaultValue!;
        }

        if (ctorInfo is MethodConstructorShapeInfo { MemberInitializers.Length: > 0 } ctor)
        {
            Debug.Assert(typeof(TArgumentState) == typeof((object?[], object?[], BitArray)));
            return (Func<TArgumentState>)(object)CreateConstructorAndMemberInitializerArgumentArrayFunc(ctor);
        }

        Debug.Assert(typeof(TArgumentState) == typeof(object?[]));
        return (Func<TArgumentState>)(object)CreateConstructorArgumentArrayFunc(ctorInfo);
    }

    public Setter<TArgumentState, TParameter> CreateConstructorArgumentStateSetter<TArgumentState, TParameter>(IConstructorShapeInfo ctorInfo, int parameterIndex)
    {
        Debug.Assert(ctorInfo.Parameters.Length > 0);

        if (ctorInfo.Parameters is [MethodParameterShapeInfo])
        {
            Debug.Assert(parameterIndex == 0);
            Debug.Assert(typeof(TArgumentState) == typeof(TParameter));
            return (Setter<TArgumentState, TParameter>)(object)new Setter<TParameter, TParameter>(
                static (ref TParameter state, TParameter param) => state = param);
        }

        if (ctorInfo is MethodConstructorShapeInfo { MemberInitializers.Length: > 0 } ctor)
        {
            Debug.Assert(typeof(TArgumentState) == typeof((object?[], object?[], BitArray)));
            if (parameterIndex < ctor.ConstructorParameters.Length)
            {
                return (Setter<TArgumentState, TParameter>)(object)new Setter<(object?[], object?[], BitArray), TParameter>(
                    (ref (object?[] ctorArgs, object?[], BitArray) state, TParameter value) => state.ctorArgs[parameterIndex] = value);
            }
            else
            {
                int initializerIndex = parameterIndex - ctor.ConstructorParameters.Length;
                return (Setter<TArgumentState, TParameter>)(object)new Setter<(object?[], object?[], BitArray), TParameter>(
                    (ref (object?[], object?[] memberArgs, BitArray flags) state, TParameter value) =>
                    {
                        state.memberArgs[initializerIndex] = value;
                        state.flags[initializerIndex] = true;
                    });
            }
        }

        Debug.Assert(typeof(TArgumentState) == typeof(object?[]));
        return (Setter<TArgumentState, TParameter>)(object)new Setter<object?[], TParameter>(
            (ref object?[] state, TParameter value) => state[parameterIndex] = value);
    }

    public Constructor<TArgumentState, TDeclaringType> CreateParameterizedConstructor<TArgumentState, TDeclaringType>(IConstructorShapeInfo ctorInfo)
    {
        Debug.Assert(ctorInfo.Parameters.Length > 0);

        if (ctorInfo is TupleConstructorShapeInfo tupleCtor)
        {
            if (ctorInfo.Parameters is [IParameterShapeInfo param])
            {
                Debug.Assert(typeof(TArgumentState) == param.Type);
                Debug.Assert(tupleCtor.NestedTupleConstructor is null);
                ConstructorInfo ctor = tupleCtor.ConstructorInfo;
                return (ref TArgumentState state) => (TDeclaringType)ctor.Invoke([state]);
            }

            Debug.Assert(typeof(TArgumentState) == typeof(object?[]));

            Stack<(ConstructorInfo, int)> ctorStack = new();
            for (TupleConstructorShapeInfo? current = tupleCtor; current != null; current = current.NestedTupleConstructor)
            {
                ctorStack.Push((current.ConstructorInfo, current.ConstructorParameters.Length));
            }

            return (Constructor<TArgumentState, TDeclaringType>)(object)new Constructor<object?[], TDeclaringType>(
                (ref object?[] state) =>
            {
                object? result = null;
                int i = state.Length;
                foreach ((ConstructorInfo ctorInfo, int arity) in ctorStack)
                {
                    object?[] localParams;
                    if (i == state.Length)
                    {
#if NET
                        localParams = state[^arity..];
#else
                        // https://github.com/Sergio0694/PolySharp/issues/104
                        localParams = new object?[arity];
                        state.AsSpan(state.Length - arity).CopyTo(localParams);
#endif
                    }
                    else
                    {
                        localParams = new object?[arity + 1];
                        state.AsSpan(i - arity, arity).CopyTo(localParams);
                        localParams[arity] = result;
                    }

                    result = ctorInfo.Invoke(localParams);
                    i -= arity;
                }

                return (TDeclaringType)result!;
            });
        }

        if (ctorInfo is MethodConstructorShapeInfo methodCtor)
        {
            MemberInitializerShapeInfo[] memberInitializers = methodCtor.MemberInitializers;
            if (memberInitializers.Length > 0)
            {
                Debug.Assert(typeof(TArgumentState) == typeof((object?[], object?[], BitArray)));

                if (methodCtor.ConstructorMethod is { } cI)
                {
                    return (Constructor<TArgumentState, TDeclaringType>)(object)new Constructor<(object?[], object?[], BitArray), TDeclaringType>(
                        (ref (object?[] ctorArgs, object?[] memberArgs, BitArray memberFlags) state) =>
                        {
                            object obj = cI.Invoke(state.ctorArgs)!;
                            PopulateMemberInitializers(obj, memberInitializers, state.memberArgs, state.memberFlags);
                            return (TDeclaringType)obj!;
                        });
                }
                else
                {
                    return (Constructor<TArgumentState, TDeclaringType>)(object)new Constructor<(object?[], object?[], BitArray), TDeclaringType>(
                        (ref (object?[] ctorArgs, object?[] memberArgs, BitArray memberFlags) state) =>
                        {
                            object obj = default(TDeclaringType)!;
                            PopulateMemberInitializers(obj, memberInitializers, state.memberArgs, state.memberFlags);
                            return (TDeclaringType)obj!;
                        });
                }

                static void PopulateMemberInitializers(object obj, MemberInitializerShapeInfo[] memberInitializers, object?[] memberArgs, BitArray memberFlags)
                {
                    for (int i = 0; i < memberInitializers.Length; i++)
                    {
                        if (!memberFlags[i])
                        {
                            continue;
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
            else
            {
                if (methodCtor.Parameters is [MethodParameterShapeInfo pI])
                {
                    DebugExt.Assert(typeof(TArgumentState) == pI.Type);
                    DebugExt.Assert(methodCtor.ConstructorMethod != null);
                    MethodBase ctor = methodCtor.ConstructorMethod;
                    return (ref TArgumentState state) => (TDeclaringType)ctor.Invoke([state])!;
                }

                Debug.Assert(typeof(TArgumentState) == typeof(object?[]));
                return methodCtor.ConstructorMethod is { } cI
                    ? (Constructor<TArgumentState, TDeclaringType>)(object)new Constructor<object?[], TDeclaringType>((ref object?[] state) => (TDeclaringType)cI.Invoke(state)!)
                    : static (ref TArgumentState _) => default!;
            }
        }

        Debug.Fail($"Unrecognized constructor shape {ctorInfo}.");
        return null!;
    }

    public TDelegate CreateFuncDelegate<TDelegate>(ConstructorInfo ctorInfo) where TDelegate : Delegate
    {
        Debug.Fail("Should not be called if not using Reflection.Emit");
        throw new NotSupportedException();
    }

    public Func<T, TResult> CreateFuncDelegate<T, TResult>(ConstructorInfo ctorInfo)
        => value => (TResult)ctorInfo.Invoke([value]);

    public Func<T1, T2, TResult> CreateFuncDelegate<T1, T2, TResult>(ConstructorInfo ctorInfo)
        => (arg1, arg2) => (TResult)ctorInfo.Invoke([arg1, arg2]);

    [DoesNotReturn]
    private static T NotReachable<T>()
    {
        Debug.Fail("This code should not be reachable.");
        throw new InvalidOperationException("This code should not be reachable.");
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

    public MutableCollectionConstructor<TKey, TDeclaringType> CreateMutableCollectionConstructor<TKey, TElement, TDeclaringType>(MutableCollectionConstructorInfo collectionCtorInfo)
    {
        Action<CollectionConstructionOptions<TKey>, object?[]>[] argumentSetters = collectionCtorInfo.Signature
            .Select(CreateArgumentSetter)
            .ToArray();

        var ctorInfo = collectionCtorInfo.DefaultConstructor;
        return (in CollectionConstructionOptions<TKey> opts) =>
        {
            var args = new object?[argumentSetters.Length];
            for (int i = 0; i < argumentSetters.Length; i++)
            {
                argumentSetters[i](opts, args);
            }

            return (TDeclaringType)ctorInfo.Invoke(args)!;
        };

        static Action<CollectionConstructionOptions<TKey>, object?[]> CreateArgumentSetter(CollectionConstructorParameter type, int index)
        {
            return type switch
            {
                CollectionConstructorParameter.Capacity => (opts, args) => args[index] = opts.Capacity ?? 4,
                CollectionConstructorParameter.CapacityOptional => (opts, args) => args[index] = opts.Capacity,
                CollectionConstructorParameter.EqualityComparer => (opts, args) => args[index] = opts.EqualityComparer ?? EqualityComparer<TKey>.Default,
                CollectionConstructorParameter.EqualityComparerOptional => (opts, args) => args[index] = opts.EqualityComparer,
                CollectionConstructorParameter.Comparer => (opts, args) => args[index] = opts.Comparer ?? Comparer<TKey>.Default,
                CollectionConstructorParameter.ComparerOptional => (opts, args) => args[index] = opts.Comparer,
                _ => throw new NotSupportedException(type.ToString()),
            };
        }
    }

    public EnumerableCollectionConstructor<TKey, TElement, TDeclaringType> CreateEnumerableCollectionConstructor<TKey, TElement, TDeclaringType>(ParameterizedCollectionConstructorInfo collectionCtorInfo)
    {
        Func<IEnumerable<TElement>, IEqualityComparer<TKey>?, IDictionary>? dictionaryFactory = null;
        Debug.Assert(collectionCtorInfo.Factory is MethodInfo { IsStatic: true } or ConstructorInfo { IsStatic: false });
        Action<IEnumerable<TElement>, CollectionConstructionOptions<TKey>, object?[]>[] argumentSetters = collectionCtorInfo.Signature
            .Select(CreateArgumentSetter)
            .ToArray();

        if (collectionCtorInfo.Factory is MethodInfo methodInfo)
        {
            return (IEnumerable<TElement> enumerable, in CollectionConstructionOptions<TKey> opts) =>
            {
                object?[] args = BuildArguments(enumerable, opts);
                return (TDeclaringType)methodInfo.Invoke(null, args)!;
            };
        }

        var ctorInfo = (ConstructorInfo)collectionCtorInfo.Factory;
        return (IEnumerable<TElement> enumerable, in CollectionConstructionOptions<TKey> opts) =>
        {
            object?[] args = BuildArguments(enumerable, opts);
            return (TDeclaringType)ctorInfo.Invoke(args)!;
        };

        object?[] BuildArguments(IEnumerable<TElement> enumerable, in CollectionConstructionOptions<TKey> opts)
        {
            var args = new object?[argumentSetters.Length];
            for (int i = 0; i < argumentSetters.Length; i++)
            {
                argumentSetters[i](enumerable, opts, args);
            }

            return args;
        }

        Action<IEnumerable<TElement>, CollectionConstructionOptions<TKey>, object?[]> CreateArgumentSetter(CollectionConstructorParameter type, int index)
        {
            return type switch
            {
                CollectionConstructorParameter.Enumerable => (enumerable, opts, args) => args[index] = enumerable,
                CollectionConstructorParameter.List => (enumerable, opts, args) => args[index] = enumerable.ToList(),
                CollectionConstructorParameter.HashSet => (enumerable, opts, args) => args[index] = CreateHashSet(enumerable, opts.EqualityComparer),
                CollectionConstructorParameter.Dictionary => (enumerable, opts, args) => args[index] = (dictionaryFactory ??= CreateDictionaryFactory())(enumerable, opts.EqualityComparer),
                CollectionConstructorParameter.Capacity => (enumerable, opts, args) => args[index] = opts.Capacity ?? 0,
                CollectionConstructorParameter.CapacityOptional => (enumerable, opts, args) => args[index] = opts.Capacity,
                CollectionConstructorParameter.EqualityComparer => (enumerable, opts, args) => args[index] = opts.EqualityComparer ?? EqualityComparer<TKey>.Default,
                CollectionConstructorParameter.EqualityComparerOptional => (enumerable, opts, args) => args[index] = opts.EqualityComparer,
                CollectionConstructorParameter.Comparer => (enumerable, opts, args) => args[index] = opts.Comparer ?? Comparer<TKey>.Default,
                CollectionConstructorParameter.ComparerOptional => (enumerable, opts, args) => args[index] = opts.Comparer,
                _ => throw new NotSupportedException(), // Not supported in the current implementation.
            };

            static HashSet<TElement> CreateHashSet(IEnumerable<TElement> enumerable, IEqualityComparer<TKey>? comparer)
            {
                var hashSet = new HashSet<TElement>((IEqualityComparer<TElement>)(object)comparer!);
                foreach (TElement element in enumerable)
                {
                    hashSet.Add(element);
                }

                return hashSet;
            }
        }

        Func<IEnumerable<TElement>, IEqualityComparer<TKey>?, IDictionary> CreateDictionaryFactory()
        {
            Debug.Assert(typeof(TElement).IsGenericType && typeof(TElement).GetGenericTypeDefinition() == typeof(KeyValuePair<,>));
            MethodInfo factory = typeof(ReflectionMemberAccessor)
                .GetMethod(nameof(CreateDictionary), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(typeof(TElement).GetGenericArguments());

            return factory.CreateDelegate<Func<IEnumerable<TElement>, IEqualityComparer<TKey>?, IDictionary>>();
        }
    }

    private static Dictionary<TKey, TValue> CreateDictionary<TKey, TValue>(IEnumerable<KeyValuePair<TKey, TValue>> entries, IEqualityComparer<TKey>? comparer)
        where TKey : notnull
    {
#if NET
        return entries.ToDictionary(comparer);
#else
        return entries.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, comparer);
#endif
    }

    public SpanCollectionConstructor<TKey, TElement, TCollection> CreateSpanCollectionConstructor<TKey, TElement, TCollection>(ParameterizedCollectionConstructorInfo constructorInfo)
    {
        if (constructorInfo is { Factory: MethodInfo factory, Signature: [CollectionConstructorParameter.Span] })
        {
            SpanFunc<TElement, TCollection> spanFunc = factory.CreateDelegate<SpanFunc<TElement, TCollection>>();
            return (ReadOnlySpan<TElement> span, in CollectionConstructionOptions<TKey> _) =>
            {
                return spanFunc(span);
            };
        }

        Debug.Fail("Should not be reached.");
        throw new NotSupportedException("Invoking methods that accept span is not supported with reflection emit disabled.");
    }

    private static Func<object?[]> CreateConstructorArgumentArrayFunc(IConstructorShapeInfo ctorInfo)
    {
        int arity = ctorInfo.Parameters.Length;
        if (arity == 0)
        {
            return static () => [];
        }
        else if (ctorInfo.Parameters.Any(param => param.HasDefaultValue))
        {
            object?[] sourceParamArray = GetDefaultParameterArray(ctorInfo.Parameters);
            return () => (object?[])sourceParamArray.Clone();
        }
        else
        {
            return () => new object?[arity];
        }
    }

    private static Func<(object?[], object?[], BitArray)> CreateConstructorAndMemberInitializerArgumentArrayFunc(MethodConstructorShapeInfo ctorInfo)
    {
        Debug.Assert(ctorInfo.MemberInitializers.Length > 0);
        int constructorParameterLength = ctorInfo.ConstructorParameters.Length;
        int memberInitializerLength = ctorInfo.MemberInitializers.Length;

        if (constructorParameterLength == 0)
        {
            return () => ([], new object?[memberInitializerLength], new BitArray(memberInitializerLength));
        }

        if (ctorInfo.ConstructorParameters.Any(param => param.HasDefaultValue))
        {
            object?[] sourceParamArray = GetDefaultParameterArray(ctorInfo.ConstructorParameters);
            return () => ((object?[])sourceParamArray.Clone(), new object?[memberInitializerLength], new BitArray(memberInitializerLength));
        }
        else
        {
            return () => (new object?[constructorParameterLength], new object?[memberInitializerLength], new BitArray(memberInitializerLength));
        }
    }

    private static object?[] GetDefaultParameterArray(IEnumerable<IParameterShapeInfo> parameters)
        => parameters.Select(p => p.DefaultValue).ToArray();

    private delegate TResult SpanFunc<TElement, TResult>(ReadOnlySpan<TElement> span);
}
