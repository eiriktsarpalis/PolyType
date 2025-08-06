using PolyType.Abstractions;
using PolyType.SourceGenModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace PolyType.ReflectionProvider.MemberAccessors;

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class ReflectionEmitMemberAccessor : IReflectionMemberAccessor
{
    public Getter<TDeclaringType, TPropertyType> CreateGetter<TDeclaringType, TPropertyType>(MemberInfo memberInfo, MemberInfo[]? parentMembers)
    {
        Debug.Assert(memberInfo is FieldInfo or PropertyInfo);
        Debug.Assert(parentMembers is null || typeof(TDeclaringType).IsNestedTupleRepresentation());

        DynamicMethod dynamicMethod = CreateDynamicMethod(memberInfo.Name, typeof(TPropertyType), [typeof(TDeclaringType).MakeByRefType()]);
        ILGenerator generator = dynamicMethod.GetILGenerator();

        // return arg0.Member;
        generator.Emit(OpCodes.Ldarg_0);
        LdRef(generator, typeof(TDeclaringType));

        if (parentMembers != null)
        {
            foreach (MemberInfo parent in parentMembers)
            {
                EmitGet(parent, isParentMember: true);
            }
        }

        EmitGet(memberInfo, isParentMember: false);
        generator.Emit(OpCodes.Ret);

        return CreateDelegate<Getter<TDeclaringType, TPropertyType>>(dynamicMethod);

        void EmitGet(MemberInfo member, bool isParentMember)
        {
            switch (member)
            {
                case PropertyInfo prop:
                    Debug.Assert(prop.CanRead);
                    Debug.Assert(!isParentMember || !prop.DeclaringType!.IsValueType);

                    MethodInfo getter = prop.GetGetMethod(true)!;
                    if (getter.DeclaringType!.IsValueType)
                    {
                        generator.EmitCall(OpCodes.Call, getter, null);
                    }
                    else
                    {
                        generator.EmitCall(OpCodes.Callvirt, getter, null);
                    }

                    break;

                case FieldInfo field:
                    if (isParentMember)
                    {
                        generator.Emit(OpCodes.Ldflda, field);
                    }
                    else
                    {
                        generator.Emit(OpCodes.Ldfld, field);
                    }

                    break;

                default:
                    Debug.Fail("Unreachable code");
                    break;
            }
        }
    }

    public Setter<TDeclaringType, TPropertyType> CreateSetter<TDeclaringType, TPropertyType>(MemberInfo memberInfo, MemberInfo[]? parentMembers)
    {
        Debug.Assert(memberInfo is FieldInfo or MemberInfo);
        Debug.Assert(parentMembers is null || typeof(TDeclaringType).IsNestedTupleRepresentation());

        DynamicMethod dynamicMethod = CreateDynamicMethod(memberInfo.Name, typeof(void), [typeof(TDeclaringType).MakeByRefType(), typeof(TPropertyType)]);
        ILGenerator generator = dynamicMethod.GetILGenerator();

        // arg0.Member = arg1;
        generator.Emit(OpCodes.Ldarg_0);
        LdRef(generator, typeof(TDeclaringType));

        if (parentMembers != null)
        {
            Debug.Assert(parentMembers is FieldInfo[]);
            foreach (FieldInfo parentField in (FieldInfo[])parentMembers)
            {
                Debug.Assert(parentField.DeclaringType!.IsValueType);
                Debug.Assert(parentField.FieldType!.IsValueType);
                generator.Emit(OpCodes.Ldflda, parentField);
            }
        }

        generator.Emit(OpCodes.Ldarg_1);

        switch (memberInfo)
        {
            case PropertyInfo prop:
                Debug.Assert(prop.CanWrite);
                MethodInfo setter = prop.GetSetMethod(true)!;
                if (typeof(TDeclaringType).IsValueType)
                {
                    generator.EmitCall(OpCodes.Call, setter, null);
                }
                else
                {
                    generator.EmitCall(OpCodes.Callvirt, setter, null);
                }

                break;

            case FieldInfo field:
                Debug.Assert(!field.IsInitOnly);
                generator.Emit(OpCodes.Stfld, field);
                break;

            default:
                Debug.Fail("Unreachable code");
                break;
        }

        generator.Emit(OpCodes.Ret);
        return CreateDelegate<Setter<TDeclaringType, TPropertyType>>(dynamicMethod);
    }

    public EnumerableAppender<TEnumerable, TElement> CreateEnumerableAppender<TEnumerable, TElement>(MethodInfo methodInfo)
    {
        DynamicMethod dynamicMethod = CreateDynamicMethod(methodInfo.Name, typeof(bool), [typeof(TEnumerable).MakeByRefType(), typeof(TElement)]);
        ILGenerator generator = dynamicMethod.GetILGenerator();

        // return arg0.Add(arg1);
        generator.Emit(OpCodes.Ldarg_0);
        LdRef(generator, typeof(TEnumerable));

        generator.Emit(OpCodes.Ldarg_1);

        if (typeof(TEnumerable).IsValueType)
        {
            generator.Emit(OpCodes.Call, methodInfo);
        }
        else
        {
            generator.Emit(OpCodes.Callvirt, methodInfo);
        }

        if (methodInfo.ReturnType != typeof(bool))
        {
            if (methodInfo.ReturnType != typeof(void))
            {
                generator.Emit(OpCodes.Pop);
            }

            generator.Emit(OpCodes.Ldc_I4_1); // Load true
        }

        generator.Emit(OpCodes.Ret);
        return CreateDelegate<EnumerableAppender<TEnumerable, TElement>>(dynamicMethod);
    }

    public DictionaryInserter<TDictionary, TKey, TValue> CreateDictionaryInserter<TDictionary, TKey, TValue>(MutableCollectionConstructorInfo ctorInfo, DictionaryInsertionMode insertionMode)
    {
        switch (insertionMode)
        {
            case DictionaryInsertionMode.Overwrite or DictionaryInsertionMode.Throw:
                DebugExt.Assert(ctorInfo.SetMethod is not null || ctorInfo.AddMethod is not null);
                var insertMethod = insertionMode is DictionaryInsertionMode.Overwrite ? ctorInfo.SetMethod : ctorInfo.AddMethod;
                return CreateAddMethodDelegate(insertMethod!);

            case DictionaryInsertionMode.Discard when ctorInfo.TryAddMethod is not null:
                return CreateTryAddMethodDelegate(ctorInfo.TryAddMethod);

            case DictionaryInsertionMode.Discard:
                DebugExt.Assert(ctorInfo.ContainsKeyMethod is not null && ctorInfo is { AddMethod: not null } or { SetMethod: not null });
                var addMethod = ctorInfo.AddMethod ?? ctorInfo.SetMethod;
                return CreateContainsKeyAddMethodDelegate(ctorInfo.ContainsKeyMethod, addMethod!);

            default:
                throw new NotSupportedException();
        }

        DictionaryInserter<TDictionary, TKey, TValue> CreateAddMethodDelegate(MethodInfo insertMethod)
        {
            DynamicMethod dynamicMethod = CreateDynamicMethod("dictionaryInserter", typeof(bool), [typeof(TDictionary).MakeByRefType(), typeof(TKey), typeof(TValue)]);
            ILGenerator generator = dynamicMethod.GetILGenerator();

            // Load dictionary reference
            generator.Emit(OpCodes.Ldarg_0);
            LdRef(generator, typeof(TDictionary));

            // Load key and value arguments
            generator.Emit(OpCodes.Ldarg_1);
            generator.Emit(OpCodes.Ldarg_2);

            // Call the insert method
            if (typeof(TDictionary).IsValueType)
            {
                generator.Emit(OpCodes.Call, insertMethod);
            }
            else
            {
                generator.Emit(OpCodes.Callvirt, insertMethod);
            }

            // Pop return value if method returns non-void
            if (insertMethod.ReturnType != typeof(void))
            {
                generator.Emit(OpCodes.Pop);
            }

            // Return true (always successful for overwrite/throw modes)
            generator.Emit(OpCodes.Ldc_I4_1);
            generator.Emit(OpCodes.Ret);

            return CreateDelegate<DictionaryInserter<TDictionary, TKey, TValue>>(dynamicMethod);
        }

        DictionaryInserter<TDictionary, TKey, TValue> CreateTryAddMethodDelegate(MethodInfo tryAddMethod)
        {
            DynamicMethod dynamicMethod = CreateDynamicMethod("dictionaryTryAdd", typeof(bool), [typeof(TDictionary).MakeByRefType(), typeof(TKey), typeof(TValue)]);
            ILGenerator generator = dynamicMethod.GetILGenerator();

            // Load dictionary reference
            generator.Emit(OpCodes.Ldarg_0);
            LdRef(generator, typeof(TDictionary));

            // Load key and value arguments
            generator.Emit(OpCodes.Ldarg_1);
            generator.Emit(OpCodes.Ldarg_2);

            // Call TryAdd method
            if (typeof(TDictionary).IsValueType)
            {
                generator.Emit(OpCodes.Call, tryAddMethod);
            }
            else
            {
                generator.Emit(OpCodes.Callvirt, tryAddMethod);
            }

            // Return the result from TryAdd
            generator.Emit(OpCodes.Ret);

            return CreateDelegate<DictionaryInserter<TDictionary, TKey, TValue>>(dynamicMethod);
        }

        DictionaryInserter<TDictionary, TKey, TValue> CreateContainsKeyAddMethodDelegate(MethodInfo containsKeyMethod, MethodInfo addMethod)
        {
            DynamicMethod dynamicMethod = CreateDynamicMethod("dictionaryContainsKeyAdd", typeof(bool), [typeof(TDictionary).MakeByRefType(), typeof(TKey), typeof(TValue)]);
            ILGenerator generator = dynamicMethod.GetILGenerator();

            Label addLabel = generator.DefineLabel();
            Label returnFalseLabel = generator.DefineLabel();

            // Check if key already exists
            generator.Emit(OpCodes.Ldarg_0);
            LdRef(generator, typeof(TDictionary));
            generator.Emit(OpCodes.Ldarg_1);

            // Call ContainsKey
            if (typeof(TDictionary).IsValueType)
            {
                generator.Emit(OpCodes.Call, containsKeyMethod);
            }
            else
            {
                generator.Emit(OpCodes.Callvirt, containsKeyMethod);
            }

            // If key exists, return false (discard mode)
            generator.Emit(OpCodes.Brtrue_S, returnFalseLabel);

            // Key doesn't exist, add it
            generator.Emit(OpCodes.Ldarg_0);
            LdRef(generator, typeof(TDictionary));
            generator.Emit(OpCodes.Ldarg_1);
            generator.Emit(OpCodes.Ldarg_2);

            // Call Add method
            if (typeof(TDictionary).IsValueType)
            {
                generator.Emit(OpCodes.Call, addMethod);
            }
            else
            {
                generator.Emit(OpCodes.Callvirt, addMethod);
            }

            // Pop return value if add method returns non-void
            if (addMethod.ReturnType != typeof(void))
            {
                generator.Emit(OpCodes.Pop);
            }

            // Return true (successfully added)
            generator.Emit(OpCodes.Ldc_I4_1);
            generator.Emit(OpCodes.Ret);

            // Return false (key already exists, discarded)
            generator.MarkLabel(returnFalseLabel);
            generator.Emit(OpCodes.Ldc_I4_0);
            generator.Emit(OpCodes.Ret);

            return CreateDelegate<DictionaryInserter<TDictionary, TKey, TValue>>(dynamicMethod);
        }
    }

    public Func<TDeclaringType> CreateDefaultConstructor<TDeclaringType>(IMethodShapeInfo ctorInfo)
    {
        Debug.Assert(ctorInfo is MethodShapeInfo { Parameters: [] });

        var methodCtor = (MethodShapeInfo)ctorInfo;
        if (methodCtor.Method is null)
        {
            return static () => default!;
        }

        DynamicMethod dynamicMethod = CreateDynamicMethod("defaultCtor", typeof(TDeclaringType), []);
        ILGenerator generator = dynamicMethod.GetILGenerator();

        // return new TDeclaringType();
        EmitCall(generator, methodCtor.Method);
        generator.Emit(OpCodes.Ret);
        return CreateDelegate<Func<TDeclaringType>>(dynamicMethod);
    }

    public Type CreateConstructorArgumentStateType(IMethodShapeInfo ctorInfo)
    {
        if (ctorInfo.Parameters is [])
        {
            return typeof(EmptyArgumentState);
        }

        Type argumentType = ctorInfo switch
        {
            { Parameters: [IParameterShapeInfo p] } => p.Type, // use the type itself for single parameter ctors.
            TupleConstructorShapeInfo { IsValueTuple: true } => ctorInfo.ReturnType, // Use the type itself as the argument state for value tuples.
            _ => ReflectionHelpers.CreateValueTupleType(ctorInfo.Parameters.Select(p => p.Type).ToArray()), // use a value tuple for multiple parameters.
        };

        return ctorInfo.Parameters.Length <= 64
            ? typeof(SmallArgumentState<>).MakeGenericType(argumentType)
            : typeof(LargeArgumentState<>).MakeGenericType(argumentType);
    }

    public Func<TArgumentState> CreateConstructorArgumentStateCtor<TArgumentState>(IMethodShapeInfo ctorInfo)
        where TArgumentState : IArgumentState
    {
        DynamicMethod dynamicMethod = CreateDynamicMethod("argumentStateCtor", typeof(TArgumentState), [typeof(StrongBox<ValueBitArray>)]);
        ILGenerator generator = dynamicMethod.GetILGenerator();

        if (ctorInfo.Parameters is [])
        {
            Debug.Assert(typeof(TArgumentState) == typeof(EmptyArgumentState));

            // return EmptyArgumentState.Instance;
            PropertyInfo singletonState = typeof(EmptyArgumentState).GetProperty(nameof(EmptyArgumentState.Instance), BindingFlags.Public | BindingFlags.Static)!;
            generator.Emit(OpCodes.Call, singletonState.GetGetMethod(true)!);
            generator.Emit(OpCodes.Ret);
            return CreateDelegate<Func<TArgumentState>>(dynamicMethod);
        }

        Debug.Assert(typeof(TArgumentState).IsGenericType);
        Type argumentsType = typeof(TArgumentState).GetGenericArguments()[0];

        if (ctorInfo.Parameters is [IParameterShapeInfo singleParameter])
        {
            Debug.Assert(argumentsType == singleParameter.Type);
            ulong requiredMask = singleParameter.IsRequired ? 1UL : 0UL;

            // new SmallArgumentState<T>(defaultValue, count: 1, requiredMask);
            if (singleParameter.HasDefaultValue)
            {
                LdLiteral(generator, singleParameter.Type, singleParameter.DefaultValue);
            }
            else
            {
                LdDefaultValue(generator, singleParameter.Type);
            }

            generator.Emit(OpCodes.Ldc_I4_1); // Argument state count
            LdLiteral(generator, typeof(ulong), requiredMask); // Required mask

            ConstructorInfo cI = typeof(TArgumentState).GetConstructors()[0]!;
            generator.Emit(OpCodes.Newobj, cI);
            generator.Emit(OpCodes.Ret);
            return CreateDelegate<Func<TArgumentState>>(dynamicMethod);
        }
        else if (ctorInfo.Parameters.Length <= 64)
        {
            Debug.Assert(typeof(TArgumentState).GetGenericTypeDefinition() == typeof(SmallArgumentState<>));
            Debug.Assert(argumentsType.IsValueTupleType());
            ulong requiredMask = ComputeRequiredMask(ctorInfo);

            // new SmallArgumentState<T>(argumentTuple, length, requiredMask);
            LdDefaultArgsAsTuple(generator, ctorInfo, argumentsType);
            LdLiteral(generator, typeof(int), ctorInfo.Parameters.Length);
            LdLiteral(generator, typeof(ulong), requiredMask);

            ConstructorInfo cI = typeof(TArgumentState).GetConstructors()[0]!;
            generator.Emit(OpCodes.Newobj, cI);
            generator.Emit(OpCodes.Ret);
            return CreateDelegate<Func<TArgumentState>>(dynamicMethod);

            static ulong ComputeRequiredMask(IMethodShapeInfo ctorInfo)
            {
                ulong mask = 0;
                for (int i = 0; i < ctorInfo.Parameters.Length; i++)
                {
                    if (ctorInfo.Parameters[i].IsRequired)
                    {
                        mask |= 1UL << i;
                    }
                }

                return mask;
            }
        }
        else
        {
            Debug.Assert(typeof(TArgumentState).GetGenericTypeDefinition() == typeof(LargeArgumentState<>));
            Debug.Assert(argumentsType.IsValueTupleType());

            // We need to box the ValueBitArray so that it can be captured by the dynamic method delegate.
            StrongBox<ValueBitArray> requiredMask = new(ComputeRequiredMask(ctorInfo));

            // new LargeArgumentState<T>(argumentTuple, length, strongBox.Value);
            LdDefaultArgsAsTuple(generator, ctorInfo, argumentsType);
            LdLiteral(generator, typeof(int), ctorInfo.Parameters.Length);
            generator.Emit(OpCodes.Ldarg_0); // Load the StrongBox parameter
            generator.Emit(OpCodes.Ldfld, requiredMask.GetType().GetField("Value")!);

            ConstructorInfo cI = typeof(TArgumentState).GetConstructors()[0]!;
            generator.Emit(OpCodes.Newobj, cI);
            generator.Emit(OpCodes.Ret);

            // Create a delegate that captures the precomputed BitArray
            return CreateDelegate<Func<TArgumentState>>(dynamicMethod, requiredMask);

            static ValueBitArray ComputeRequiredMask(IMethodShapeInfo ctorInfo)
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
        }

        static void LdDefaultArgsAsTuple(ILGenerator generator, IMethodShapeInfo ctorInfo, Type tupleType)
        {
            Debug.Assert(tupleType.IsValueTupleType());

            if (ctorInfo.Parameters.All(p => !p.HasDefaultValue))
            {
                // Load the arguments tuple as a single default instruction.
                LdDefaultValue(generator, tupleType);
                return;
            }

            // Load individual arguments with default values.
            foreach (IParameterShapeInfo param in ctorInfo.Parameters)
            {
                if (param.HasDefaultValue)
                {
                    object? defaultValue = param.DefaultValue;
                    LdLiteral(generator, param.Type, defaultValue);
                }
                else
                {
                    LdDefaultValue(generator, param.Type);
                }
            }

            // Call the final tuple ctor(s).
            EmitTupleCtor(generator, tupleType, ctorInfo.Parameters.Length);

            static void EmitTupleCtor(ILGenerator generator, Type tupleType, int arity)
            {
                if (arity > 7)
                {
                    // the tuple nests more tuple types
                    // NB emit NewObj calls starting with innermost type first
                    EmitTupleCtor(generator, tupleType.GetGenericArguments()[7], arity - 7);
                }

                ConstructorInfo ctorInfo = tupleType.GetConstructors()[0]!;
                generator.Emit(OpCodes.Newobj, ctorInfo);
            }
        }
    }

    public Setter<TArgumentState, TParameter> CreateArgumentStateSetter<TArgumentState, TParameter>(IMethodShapeInfo ctorInfo, int parameterIndex)
        where TArgumentState : IArgumentState
    {
        Debug.Assert(ctorInfo.Parameters.Length > 0);
        Debug.Assert(typeof(TArgumentState).IsGenericType);
        Type argumentsType = typeof(TArgumentState).GetGenericArguments()[0];
        Debug.Assert(ctorInfo.Parameters.Length == 1 ? argumentsType == ctorInfo.Parameters[0].Type : argumentsType.IsValueTupleType());

        DynamicMethod dynamicMethod = CreateDynamicMethod("argumentStateSetter", typeof(void), [typeof(TArgumentState).MakeByRefType(), typeof(TParameter)]);
        ILGenerator generator = dynamicMethod.GetILGenerator();

        // arg0.Arguments(.ItemN)* = arg1;
        FieldInfo argumentsField = typeof(TArgumentState).GetField("Arguments", BindingFlags.Public | BindingFlags.Instance)!;
        if (ctorInfo.Parameters.Length == 1)
        {
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldarg_1);
            generator.Emit(OpCodes.Stfld, argumentsField);
        }
        else
        {
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldflda, argumentsField);
            FieldInfo nestedField = LdNestedTuple(generator, argumentsType, parameterIndex);
            generator.Emit(OpCodes.Ldarg_1);
            generator.Emit(OpCodes.Stfld, nestedField);
        }

        // arg0.MarkArgumentSet(index);
        generator.Emit(OpCodes.Ldarg_0);
        LdRef(generator, typeof(TArgumentState));
        LdLiteral(generator, typeof(int), parameterIndex);
        MethodInfo isArgumentSetMethod = typeof(TArgumentState).GetMethod("MarkArgumentSet", BindingFlags.Public | BindingFlags.Instance)!;
        EmitCall(generator, isArgumentSetMethod);

        generator.Emit(OpCodes.Ret);

        return CreateDelegate<Setter<TArgumentState, TParameter>>(dynamicMethod);
    }

    public Constructor<TArgumentState, TDeclaringType> CreateParameterizedConstructor<TArgumentState, TDeclaringType>(IMethodShapeInfo ctorInfo)
        where TArgumentState : IArgumentState
    {
        Debug.Assert(ctorInfo.Parameters.Length > 0);
        return CreateDelegate<Constructor<TArgumentState, TDeclaringType>>(EmitParameterizedConstructorMethod(typeof(TDeclaringType), typeof(TArgumentState), ctorInfo));
    }

    private static DynamicMethod EmitParameterizedConstructorMethod(Type declaringType, Type argumentStateType, IMethodShapeInfo ctorInfo)
    {
        Debug.Assert(ctorInfo.Parameters.Length > 0);
        Debug.Assert(argumentStateType.IsGenericType);
        Type argumentsType = argumentStateType.GetGenericArguments()[0];
        Debug.Assert(ctorInfo.Parameters.Length == 1 ? argumentsType == ctorInfo.Parameters[0].Type : argumentsType.IsValueTupleType());

        DynamicMethod dynamicMethod = CreateDynamicMethod("parameterizedCtor", declaringType, [argumentStateType.MakeByRefType()]);
        ILGenerator generator = dynamicMethod.GetILGenerator();
        FieldInfo argumentsField = argumentStateType.GetField("Arguments", BindingFlags.Public | BindingFlags.Instance)!;
        (string LogicalName, MemberInfo Member, MemberInfo[]? ParentMembers)?[] elementPaths = ctorInfo.Parameters.Length > 1
            ? ReflectionHelpers.EnumerateTupleMemberPaths(argumentsType).Select(f => ((string, MemberInfo, MemberInfo[]?)?)f).ToArray()
            : [null];

        if (ctorInfo is MethodShapeInfo methodCtor)
        {
            if (methodCtor.MemberInitializers.Length == 0)
            {
                // No member initializers -- just load all tuple elements and call the constructor
                DebugExt.Assert(methodCtor.Method is not null, "Structs without a constructor must have member initializers.");

                // return new TDeclaringType(state.Item1, state.Item2, ...);
                int i = 0;
                foreach (var elementPath in elementPaths)
                {
                    bool isByRefParam = methodCtor.Parameters[i++].IsByRef;

                    generator.Emit(OpCodes.Ldarg_0);
                    LdFromArgState(generator, ctorInfo, argumentsField, elementPath, isByRefParameter: isByRefParam);
                }

                EmitCall(generator, methodCtor.Method);
                generator.Emit(OpCodes.Ret);
            }
            else
            {
                // Emit parameterized constructor + member initializers

                // var local = new TDeclaringType(state.Item1, state.Item2, ...);
                LocalBuilder local = generator.DeclareLocal(declaringType);
                if (methodCtor.Method is null)
                {
                    Debug.Assert(declaringType.IsValueType);
                    generator.Emit(OpCodes.Ldloca_S, local);
                }

                int i = 0;
                for (; i < methodCtor.ConstructorParameters.Length; i++)
                {
                    generator.Emit(OpCodes.Ldarg_0);
                    LdFromArgState(generator, ctorInfo, argumentsField, elementPaths[i], isByRefParameter: methodCtor.ConstructorParameters[i].IsByRef);
                }

                if (methodCtor.Method is null)
                {
                    Debug.Assert(declaringType.IsValueType);
                    generator.Emit(OpCodes.Initobj, declaringType);
                }
                else
                {
                    EmitCall(generator, methodCtor.Method);
                    generator.Emit(OpCodes.Stloc, local);
                }

                // Emit optional member initializers
                MethodInfo isArgumentSetMethod = argumentStateType.GetMethod("IsArgumentSet", BindingFlags.Public | BindingFlags.Instance)!;
                foreach (MemberInitializerShapeInfo member in methodCtor.MemberInitializers)
                {
                    // if (state.IsArgumentSet(memberIndex)) local.member = state.ItemN;
                    Label label = generator.DefineLabel();

                    // if (state.IsArgumentSet(memberIndex))
                    generator.Emit(OpCodes.Ldarg_0);
                    LdLiteral(generator, typeof(int), i);
                    EmitCall(generator, isArgumentSetMethod);
                    generator.Emit(OpCodes.Brfalse_S, label);

                    // local.member = state.ItemN;
                    generator.Emit(declaringType.IsValueType ? OpCodes.Ldloca_S : OpCodes.Ldloc, local);
                    generator.Emit(OpCodes.Ldarg_0);
                    LdFromArgState(generator, ctorInfo, argumentsField, elementPaths[i], isByRefParameter: false);
                    StMember(member);
                    generator.MarkLabel(label);

                    i++;
                }

                generator.Emit(declaringType.IsValueType ? OpCodes.Ldloc_S : OpCodes.Ldloc, local);
                generator.Emit(OpCodes.Ret);
            }
        }
        else if (ctorInfo is TupleConstructorShapeInfo { IsValueTuple: true })
        {
            Debug.Assert(argumentsType == declaringType, "Tuple constructors for value tuples should use the same type as the argument type.");

            // return state.Arguments;
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldfld, argumentsField);
            generator.Emit(OpCodes.Ret);
        }
        else if (ctorInfo is TupleConstructorShapeInfo tupleCtor)
        {
            Debug.Assert(argumentsType.IsValueTupleType());

            // return new Tuple<..,Tuple<..,Tuple<..>>>(state.Item1, state.Item2, ...);
            foreach (var elementPath in elementPaths)
            {
                generator.Emit(OpCodes.Ldarg_0);
                LdFromArgState(generator, ctorInfo, argumentsField, elementPath, isByRefParameter: false);
            }

            var constructors = new Stack<ConstructorInfo>();
            for (TupleConstructorShapeInfo? curr = tupleCtor; curr != null; curr = curr.NestedTupleConstructor)
            {
                constructors.Push(curr.ConstructorInfo);
            }

            foreach (ConstructorInfo ctor in constructors)
            {
                generator.Emit(OpCodes.Newobj, ctor);
            }

            generator.Emit(OpCodes.Ret);
        }

        return dynamicMethod;

        void StMember(MemberInitializerShapeInfo memberInitializer)
        {
            switch (memberInitializer.MemberInfo)
            {
                case PropertyInfo prop:
                    DebugExt.Assert(prop.SetMethod != null);
                    OpCode callOp = declaringType.IsValueType ? OpCodes.Call : OpCodes.Callvirt;
                    generator.Emit(callOp, prop.SetMethod);
                    break;

                case FieldInfo field:
                    generator.Emit(OpCodes.Stfld, field);
                    break;

                default:
                    Debug.Fail("Invalid member");
                    break;
            }
        }
    }

    public MethodInvoker<TDeclaringType, TArgumentState, TResult> CreateMethodInvoker<TDeclaringType, TArgumentState, TResult>(MethodShapeInfo ctorInfo) where TArgumentState : IArgumentState
    {
        return CreateDelegate<MethodInvoker<TDeclaringType, TArgumentState, TResult>>(EmitMethodInvoker<TResult>(typeof(TDeclaringType), typeof(TArgumentState), ctorInfo));
    }

    private DynamicMethod EmitMethodInvoker<TResult>(Type declaringType, Type argumentStateType, MethodShapeInfo methodShapeInfo)
    {
        Debug.Assert(methodShapeInfo.Method is MethodInfo);
        var methodInfo = (MethodInfo)methodShapeInfo.Method!;

        DynamicMethod dynamicMethod = CreateDynamicMethod("methodInvoker", typeof(ValueTask<TResult>), [declaringType.MakeByRefType(), argumentStateType.MakeByRefType()]);
        ILGenerator generator = dynamicMethod.GetILGenerator();

        if (!methodInfo.IsStatic)
        {
            generator.Emit(OpCodes.Ldarg_0);
            LdRef(generator, declaringType);
        }

        if (methodShapeInfo.Parameters.Length > 0)
        {
            Debug.Assert(argumentStateType.IsGenericType);
            Type argumentsType = argumentStateType.GetGenericArguments()[0];
            FieldInfo argumentsField = argumentStateType.GetField("Arguments", BindingFlags.Public | BindingFlags.Instance)!;
            (string LogicalName, MemberInfo Member, MemberInfo[]? ParentMembers)?[] elementPaths = methodShapeInfo.Parameters.Length > 1
                ? ReflectionHelpers.EnumerateTupleMemberPaths(argumentsType).Select(f => ((string, MemberInfo, MemberInfo[]?)?)f).ToArray()
                : [null];

            int i = 0;
            foreach (var elementPath in elementPaths)
            {
                bool isByRefParam = methodShapeInfo.Parameters[i++].IsByRef;
                generator.Emit(OpCodes.Ldarg_1); // The argument state is the second argument
                LdFromArgState(generator, methodShapeInfo, argumentsField, elementPath, isByRefParameter: isByRefParam);
            }
        }

        EmitCall(generator, methodInfo);

        // Map the result to a ValueTask<TResult>
        if (methodInfo.ReturnType == typeof(void))
        {
            Debug.Assert(Unit.Value == default);
            LdDefaultValue(generator, typeof(ValueTask<TResult>));
        }
        else if (methodInfo.ReturnType == typeof(Task))
        {
            // Wrap the result in a Task<Unit>
            MethodInfo fromTaskAsyncMethod = typeof(Unit).GetMethod(nameof(Unit.FromTaskAsync), BindingFlags.Public | BindingFlags.Static)!;
            generator.Emit(OpCodes.Call, fromTaskAsyncMethod);
        }
        else if (methodInfo.ReturnType == typeof(ValueTask))
        {
            // Wrap the result in a ValueTask<Unit>
            MethodInfo fromValueTaskAsyncMethod = typeof(Unit).GetMethod(nameof(Unit.FromValueTaskAsync), BindingFlags.Public | BindingFlags.Static)!;
            generator.Emit(OpCodes.Call, fromValueTaskAsyncMethod);
        }
        else if (methodInfo.ReturnType == typeof(Task<TResult>))
        {
            // Wrap the result in a ValueTask<TResult>
            ConstructorInfo ctor = typeof(ValueTask<TResult>).GetConstructor([typeof(Task<TResult>)])!;
            generator.Emit(OpCodes.Newobj, ctor);
        }
        else if (methodInfo.ReturnType != typeof(ValueTask<TResult>))
        {
            Type returnType = methodInfo.ReturnType;
            if (returnType.IsByRef)
            {
                returnType = returnType.GetElementType()!;
                LdRef(generator, returnType, copyValueTypes: true);
            }

            Debug.Assert(returnType == typeof(TResult));
            // Wrap the result in a ValueTask<TResult>
            ConstructorInfo ctor = typeof(ValueTask<TResult>).GetConstructor([typeof(TResult)])!;
            generator.Emit(OpCodes.Newobj, ctor);
        }

        generator.Emit(OpCodes.Ret);
        return dynamicMethod;
    }

    private static void LdFromArgState(
        ILGenerator generator,
        IMethodShapeInfo methodInfo,
        FieldInfo argumentsField,
        (string LogicalName, MemberInfo Member, MemberInfo[]? ParentMembers)? tupleElement,
        bool isByRefParameter)
    {
        OpCode ldfldOpCode = isByRefParameter ? OpCodes.Ldflda : OpCodes.Ldfld;

        if (tupleElement is null)
        {
            Debug.Assert(methodInfo.Parameters.Length == 1);
            generator.Emit(ldfldOpCode, argumentsField);
            return;
        }

        Debug.Assert(tupleElement.Value.Member is FieldInfo);
        Debug.Assert(tupleElement.Value.ParentMembers is null or FieldInfo[]);

        generator.Emit(OpCodes.Ldflda, argumentsField);
        if (tupleElement.Value.ParentMembers is FieldInfo[] parentMembers)
        {
            foreach (FieldInfo parent in parentMembers)
            {
                generator.Emit(OpCodes.Ldflda, parent);
            }
        }

        generator.Emit(ldfldOpCode, (FieldInfo)tupleElement.Value.Member);
    }

    public Getter<TUnion, int> CreateGetUnionCaseIndex<TUnion>(DerivedTypeInfo[] derivedTypeInfos)
    {
        Debug.Assert(!typeof(TUnion).IsValueType);
        Debug.Assert(derivedTypeInfos.Length > 0);

        // Creates a topological sort of all cases from most derived to least derived
        // and then emits a switch statement in that order to obtain the correct index.

        // 1. Pre-process attribute data.
        Dictionary<Type, int> typesAndIndices = new();
        int defaultIndex = -1;
        foreach (DerivedTypeInfo derivedTypeInfo in derivedTypeInfos)
        {
            if (derivedTypeInfo.Type == typeof(TUnion))
            {
                defaultIndex = derivedTypeInfo.Index;
            }
            else
            {
                typesAndIndices.Add(derivedTypeInfo.Type, derivedTypeInfo.Index);
            }
        }

        // 2. Perform the topological sort.
        Type[] sortedTypes = CommonHelpers.TraverseGraphWithTopologicalSort(typeof(TUnion), GetSubtypes);
        Debug.Assert(sortedTypes.Length > 0 && sortedTypes[0] == typeof(TUnion));
        Array.Reverse(sortedTypes);

        IReadOnlyCollection<Type> GetSubtypes(Type type)
        {
            List<Type> descendants = new();
            foreach (Type derivedType in typesAndIndices.Keys)
            {
                if (derivedType != type && type.IsAssignableFrom(derivedType))
                {
                    descendants.Add(derivedType);
                }
            }

            return descendants;
        }

        // 3. Emit the dynamic method.
        DynamicMethod dynamicMethod = CreateDynamicMethod("getUnionCaseIndex", typeof(int), [typeof(TUnion).MakeByRefType()]);
        ILGenerator generator = dynamicMethod.GetILGenerator();

        var localValue = generator.DeclareLocal(typeof(TUnion));
        var localResult = generator.DeclareLocal(typeof(int));

        generator.Emit(OpCodes.Ldarg_0);
        generator.Emit(OpCodes.Ldind_Ref);
        generator.Emit(OpCodes.Stloc, localValue);

        // Emit the type test opcodes
        Label[] labels = new Label[sortedTypes.Length - 1];
        for (int i = 0; i < sortedTypes.Length - 1; i++)
        {
            generator.Emit(OpCodes.Ldloc, localValue);
            generator.Emit(OpCodes.Isinst, sortedTypes[i]);
            generator.Emit(OpCodes.Brtrue_S, labels[i] = generator.DefineLabel());
        }

        Label defaultCaseLabel = generator.DefineLabel();
        generator.Emit(OpCodes.Br_S, defaultCaseLabel);

        // Emit the result mapping opcodes
        Label returnLabel = generator.DefineLabel();
        for (int i = 0; i < labels.Length; i++)
        {
            generator.MarkLabel(labels[i]);
            generator.Emit(OpCodes.Ldc_I4, typesAndIndices[sortedTypes[i]]);
            generator.Emit(OpCodes.Stloc, localResult);
            generator.Emit(OpCodes.Br_S, returnLabel);
        }

        generator.MarkLabel(defaultCaseLabel);
        generator.Emit(OpCodes.Ldc_I4, defaultIndex);
        generator.Emit(OpCodes.Stloc, localResult);
        generator.Emit(OpCodes.Br_S, returnLabel);

        generator.MarkLabel(returnLabel);
        generator.Emit(OpCodes.Ldloc, localResult);
        generator.Emit(OpCodes.Ret);

        return CreateDelegate<Getter<TUnion, int>>(dynamicMethod);
    }

    private static DynamicMethod CreateDynamicMethod(string name, Type returnType, Type[] parameters)
        => new(name, returnType, parameters, typeof(ReflectionEmitMemberAccessor).Module, skipVisibility: true);

    private static TDelegate CreateDelegate<TDelegate>(DynamicMethod dynamicMethod, object? target = null)
        where TDelegate : Delegate
        => (TDelegate)dynamicMethod.CreateDelegate(typeof(TDelegate), target);

    private static void LdDefaultValue(ILGenerator generator, Type type)
    {
        // Load the default value for the type onto the stack
        if (!type.IsValueType)
        {
            generator.Emit(OpCodes.Ldnull);
        }
        else if (
            type == typeof(bool) || type == typeof(byte) || type == typeof(sbyte) ||
            type == typeof(char) || type == typeof(ushort) || type == typeof(short) ||
            type == typeof(int) || type == typeof(uint))
        {
            generator.Emit(OpCodes.Ldc_I4_0);
        }
        else if (type == typeof(long) || type == typeof(ulong))
        {
            generator.Emit(OpCodes.Ldc_I4_0);
            generator.Emit(OpCodes.Conv_I8);
        }
        else if (type == typeof(float))
        {
            generator.Emit(OpCodes.Ldc_R4, 0f);
        }
        else if (type == typeof(double))
        {
            generator.Emit(OpCodes.Ldc_R8, 0d);
        }
        else if (type == typeof(IntPtr))
        {
            generator.Emit(OpCodes.Ldc_I4_0);
            generator.Emit(OpCodes.Conv_I);
        }
        else if (type == typeof(UIntPtr))
        {
            generator.Emit(OpCodes.Ldc_I4_0);
            generator.Emit(OpCodes.Conv_U);
        }
        else
        {
            LocalBuilder local = generator.DeclareLocal(type);
            generator.Emit(OpCodes.Ldloca_S, local.LocalIndex);
            generator.Emit(OpCodes.Initobj, type);
            generator.Emit(OpCodes.Ldloc, local.LocalIndex);
        }
    }

    private static FieldInfo LdNestedTuple(ILGenerator generator, Type tupleType, int parameterIndex)
    {
        while (parameterIndex > 6)
        {
            // The element we want to access is in a nested tuple
            FieldInfo restField = tupleType.GetField("Rest", BindingFlags.Public | BindingFlags.Instance)!;
            generator.Emit(OpCodes.Ldflda, restField);
            tupleType = restField.FieldType;
            parameterIndex -= 7;
        }

        return tupleType.GetField($"Item{parameterIndex + 1}", BindingFlags.Public | BindingFlags.Instance)!;
    }

    private static void EmitCall(ILGenerator generator, MethodBase methodBase)
    {
        Debug.Assert(methodBase is MethodInfo or ConstructorInfo);
        if (methodBase is ConstructorInfo ctorInfo)
        {
            generator.Emit(OpCodes.Newobj, ctorInfo);
        }
        else
        {
            var methodInfo = (MethodInfo)methodBase!;
            OpCode opc = methodInfo.DeclaringType!.IsValueType || methodInfo.IsStatic
                ? OpCodes.Call
                : OpCodes.Callvirt;

            generator.Emit(opc, methodInfo);
        }
    }

    private static void LdLiteral(ILGenerator generator, Type type, object? value)
    {
        if (type.IsEnum)
        {
            type = Enum.GetUnderlyingType(type);
            value = Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
            LdLiteral(generator, type, value);
            return;
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            if (value is null)
            {
                LocalBuilder local = generator.DeclareLocal(type);
                generator.Emit(OpCodes.Ldloca_S, local.LocalIndex);
                generator.Emit(OpCodes.Initobj, type);
                generator.Emit(OpCodes.Ldloc, local.LocalIndex);
            }
            else
            {
                Type elementType = type.GetGenericArguments()[0];
                Debug.Assert(value.GetType() != type);
                ConstructorInfo ctorInfo = type.GetConstructor([elementType])!;
                LdLiteral(generator, elementType, value);
                generator.Emit(OpCodes.Newobj, ctorInfo);
            }

            return;
        }

        switch (value)
        {
            case null:
                generator.Emit(OpCodes.Ldnull);
                break;
            case bool b:
                generator.Emit(OpCodes.Ldc_I4, b ? 1 : 0);
                break;
            case byte b:
                generator.Emit(OpCodes.Ldc_I4_S, b);
                break;
            case sbyte b:
                generator.Emit(OpCodes.Ldc_I4_S, b);
                break;
            case char c:
                generator.Emit(OpCodes.Ldc_I4, c);
                break;
            case ushort s:
                generator.Emit(OpCodes.Ldc_I4_S, s);
                break;
            case short s:
                generator.Emit(OpCodes.Ldc_I4_S, s);
                break;
            case int i:
                generator.Emit(OpCodes.Ldc_I4, i);
                break;
            case uint i:
                generator.Emit(OpCodes.Ldc_I4, i);
                break;
            case long i:
                generator.Emit(OpCodes.Ldc_I8, i);
                break;
            case ulong i:
                generator.Emit(OpCodes.Ldc_I8, (long)i);
                break;
            case float f:
                generator.Emit(OpCodes.Ldc_R4, f);
                break;
            case double d:
                generator.Emit(OpCodes.Ldc_R8, d);
                break;
            case string s:
                generator.Emit(OpCodes.Ldstr, s);
                break;
            case IntPtr ptr:
                generator.Emit(OpCodes.Ldc_I8, checked((long)ptr));
                generator.Emit(OpCodes.Conv_I);
                break;
            case UIntPtr ptr:
                generator.Emit(OpCodes.Ldc_I8, checked((ulong)ptr));
                generator.Emit(OpCodes.Conv_U);
                break;

            case decimal d:
                ConstructorInfo ctor = typeof(decimal).GetConstructor([typeof(int), typeof(int), typeof(int), typeof(bool), typeof(byte)])!;
                int[] bits = decimal.GetBits(d);
                bool sign = (bits[3] & 0x80000000) != 0;
                byte scale = (byte)(bits[3] >> 16 & 0x7F);

                generator.Emit(OpCodes.Ldc_I4, bits[0]);
                generator.Emit(OpCodes.Ldc_I4, bits[1]);
                generator.Emit(OpCodes.Ldc_I4, bits[2]);
                generator.Emit(OpCodes.Ldc_I4, sign ? 1 : 0);
                generator.Emit(OpCodes.Ldc_I4_S, scale);
                generator.Emit(OpCodes.Newobj, ctor);
                break;

            case ValueType when value.Equals(ReflectionHelpers.GetDefaultValue(type)):
                // Struct argument set to `default` value
                LocalBuilder localStruct = generator.DeclareLocal(type);
                generator.Emit(OpCodes.Ldloca_S, localStruct.LocalIndex);
                generator.Emit(OpCodes.Initobj, type);
                generator.Emit(OpCodes.Ldloc, localStruct.LocalIndex);
                break;

            default:
                throw new NotImplementedException($"Default parameter support for {value.GetType()}");
        }
    }

    private static void LdRef(ILGenerator generator, Type type, bool copyValueTypes = false)
    {
        if (GetLdIndOpCode(type) is { } opcode)
        {
            generator.Emit(opcode);
        }
        else if (copyValueTypes || type.IsNullableStruct())
        {
            generator.Emit(OpCodes.Ldobj, type);
        }

        static OpCode? GetLdIndOpCode(Type argumentType)
        {
            if (!argumentType.IsValueType)
            {
                return OpCodes.Ldind_Ref;
            }

            if (argumentType.IsEnum)
            {
                argumentType = Enum.GetUnderlyingType(argumentType);
            }

            return Type.GetTypeCode(argumentType) switch
            {
                TypeCode.Boolean or TypeCode.Byte => OpCodes.Ldind_U1,
                TypeCode.SByte => OpCodes.Ldind_I1,
                TypeCode.Char or TypeCode.UInt16 => OpCodes.Ldind_U2,
                TypeCode.Int16 => OpCodes.Ldind_I2,
                TypeCode.UInt32 => OpCodes.Ldind_U4,
                TypeCode.Int32 => OpCodes.Ldind_I4,
                TypeCode.UInt64 or TypeCode.Int64 => OpCodes.Ldind_I8,
                TypeCode.Single => OpCodes.Ldind_R4,
                TypeCode.Double => OpCodes.Ldind_R8,
                _ => null,
            };
        }
    }

    public bool IsCollectionConstructorSupported(MethodBase method, CollectionConstructorParameter[] signature) => true;

    public MutableCollectionConstructor<TKey, TDeclaringType> CreateMutableCollectionConstructor<TKey, TElement, TDeclaringType>(MutableCollectionConstructorInfo ctorInfo)
    {
        // Only options parameter
        var dyn = CreateCollectionConstructorDelegate(
            "MutableCollectionCtor",
            ctorInfo.Factory,
            ctorInfo.Signature,
            keyType: typeof(TKey),
            elementType: typeof(TElement),
            [typeof(CollectionConstructionOptions<TKey>).MakeByRefType()],
            typeof(TDeclaringType));

        return CreateDelegate<MutableCollectionConstructor<TKey, TDeclaringType>>(dyn);
    }

    public ParameterizedCollectionConstructor<TKey, TElement, TCollection> CreateParameterizedCollectionConstructor<TKey, TElement, TCollection>(ParameterizedCollectionConstructorInfo constructorInfo)
    {
        // (ReadOnlySpan<TElement> values, in CollectionConstructionOptions<TKey> options)
        var dyn = CreateCollectionConstructorDelegate(
            "SpanCollectionCtor",
            constructorInfo.Factory,
            constructorInfo.Signature,
            keyType: typeof(TKey),
            elementType: typeof(TElement),
            parameterTypes: [typeof(ReadOnlySpan<TElement>), typeof(CollectionConstructionOptions<TKey>).MakeByRefType()],
            returnType: typeof(TCollection));

        return CreateDelegate<ParameterizedCollectionConstructor<TKey, TElement, TCollection>>(dyn);
    }

    private static DynamicMethod CreateCollectionConstructorDelegate(
        string name,
        MethodBase factory,
        CollectionConstructorParameter[] signature,
        Type keyType,
        Type elementType,
        Type[] parameterTypes,
        Type returnType)
    {
        var dynamicMethod = CreateDynamicMethod(name, returnType, parameterTypes);
        var il = dynamicMethod.GetILGenerator();

        Type optionsType = parameterTypes[^1];
        Debug.Assert(optionsType.IsByRef && optionsType.GetElementType()?.GetGenericTypeDefinition() == typeof(CollectionConstructionOptions<>));
        optionsType = optionsType.GetElementType()!;

        foreach (CollectionConstructorParameter parameterType in signature)
        {
            switch (parameterType)
            {
                case CollectionConstructorParameter.Span:
                    // Load the collection parameter as-is
                    il.Emit(OpCodes.Ldarg_0);
                    break;

                case CollectionConstructorParameter.List:
                    // Convert the span to List<T> using CollectionHelpers.CreateList before loading into the stack
                    Debug.Assert(parameterTypes.Length == 2 && parameterTypes[0].GetGenericTypeDefinition() == typeof(ReadOnlySpan<>));
                    MethodInfo createListMethod = typeof(CollectionHelpers).GetMethod(nameof(CollectionHelpers.CreateList), BindingFlags.Public | BindingFlags.Static)!;

                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Call, createListMethod.MakeGenericMethod(elementType));
                    break;

                case CollectionConstructorParameter.HashSet:
                    // Convert the span to HashSet<T> using CollectionHelpers.CreateHashSet before loading onto the stack
                    Debug.Assert(parameterTypes.Length == 2 && parameterTypes[0].GetGenericTypeDefinition() == typeof(ReadOnlySpan<>));
                    MethodInfo createHashSetMethod = typeof(CollectionHelpers).GetMethod(nameof(CollectionHelpers.CreateHashSet), BindingFlags.Public | BindingFlags.Static)!;
                    il.Emit(OpCodes.Ldarg_0);
                    LdOptionsProperty(nameof(CollectionConstructionOptions<int>.EqualityComparer));
                    il.Emit(OpCodes.Call, createHashSetMethod.MakeGenericMethod(keyType));
                    break;

                case CollectionConstructorParameter.Dictionary:
                    Debug.Assert(parameterTypes.Length == 2 && parameterTypes[0].GetGenericTypeDefinition() == typeof(ReadOnlySpan<>));
                    DebugExt.Assert(elementType.IsGenericType && elementType.GetGenericTypeDefinition() == typeof(KeyValuePair<,>));
                    // Convert the span to Dictionary<T> using CollectionHelpers.CreateDictionary before loading onto the stack
                    MethodInfo createDictionaryMethod = typeof(CollectionHelpers).GetMethod(nameof(CollectionHelpers.CreateDictionary), BindingFlags.Public | BindingFlags.Static)!;

                    il.Emit(OpCodes.Ldarg_0);
                    LdOptionsProperty(nameof(CollectionConstructionOptions<int>.EqualityComparer));
                    il.Emit(OpCodes.Call, createDictionaryMethod.MakeGenericMethod(elementType.GetGenericArguments()));
                    break;

                case CollectionConstructorParameter.TupleEnumerable:
                    Debug.Assert(parameterTypes.Length == 2 && parameterTypes[0].GetGenericTypeDefinition() == typeof(ReadOnlySpan<>));
                    DebugExt.Assert(elementType.IsGenericType && elementType.GetGenericTypeDefinition() == typeof(KeyValuePair<,>));
                    // Convert the span to Tuple<TKey,TValue>[] using CollectionHelpers.CreateTupleArray before loading onto the stack
                    MethodInfo createTupleArrayMethod = typeof(CollectionHelpers).GetMethod(nameof(CollectionHelpers.CreateTupleArray), BindingFlags.Public | BindingFlags.Static)!;

                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Call, createTupleArrayMethod.MakeGenericMethod(elementType.GetGenericArguments()));
                    break;

                case CollectionConstructorParameter.Capacity:
                case CollectionConstructorParameter.CapacityOptional:
                {
                    LdOptionsProperty(nameof(CollectionConstructionOptions<int>.Capacity));

                    LocalBuilder nullable = il.DeclareLocal(typeof(int?));
                    il.Emit(OpCodes.Stloc, nullable);
                    il.Emit(OpCodes.Ldloca_S, nullable);

                    MethodInfo getValueOrDefault = typeof(int?).GetMethod("GetValueOrDefault", Type.EmptyTypes)!;
                    il.Emit(OpCodes.Call, getValueOrDefault);
                    break;
                }

                case CollectionConstructorParameter.EqualityComparer:
                case CollectionConstructorParameter.EqualityComparerOptional:
                {
                    LdOptionsProperty(nameof(CollectionConstructionOptions<int>.EqualityComparer));

                    if (parameterType is CollectionConstructorParameter.EqualityComparer)
                    {
                        // For required parameter, replace null with EqualityComparer<TKey>.Default
                        Label notNullLabel = il.DefineLabel();
                        Label endLabel = il.DefineLabel();

                        il.Emit(OpCodes.Dup);
                        il.Emit(OpCodes.Brtrue_S, notNullLabel);

                        // Value is null, pop it and load default
                        il.Emit(OpCodes.Pop);
                        Type equalityComparerType = typeof(EqualityComparer<>).MakeGenericType(keyType);
                        PropertyInfo defaultProperty = equalityComparerType.GetProperty("Default", BindingFlags.Public | BindingFlags.Static)!;
                        il.Emit(OpCodes.Call, defaultProperty.GetGetMethod()!);
                        il.Emit(OpCodes.Br_S, endLabel);

                        il.MarkLabel(notNullLabel);
                        // Value is not null, use it as-is
                        il.MarkLabel(endLabel);
                    }

                    // For optional parameter, pass null as-is
                    break;
                }

                case CollectionConstructorParameter.Comparer:
                case CollectionConstructorParameter.ComparerOptional:
                {
                    LdOptionsProperty(nameof(CollectionConstructionOptions<int>.Comparer));

                    if (parameterType is CollectionConstructorParameter.Comparer)
                    {
                        // For required parameter, replace null with Comparer<TKey>.Default
                        Label notNullLabel = il.DefineLabel();
                        Label endLabel = il.DefineLabel();

                        il.Emit(OpCodes.Dup);
                        il.Emit(OpCodes.Brtrue_S, notNullLabel);

                        // Value is null, pop it and load default
                        il.Emit(OpCodes.Pop);
                        Type comparerType = typeof(Comparer<>).MakeGenericType(keyType);
                        PropertyInfo defaultProperty = comparerType.GetProperty("Default", BindingFlags.Public | BindingFlags.Static)!;
                        il.Emit(OpCodes.Call, defaultProperty.GetGetMethod()!);
                        il.Emit(OpCodes.Br_S, endLabel);

                        il.MarkLabel(notNullLabel);
                        // Value is not null, use it as-is
                        il.MarkLabel(endLabel);
                    }

                    // For optional parameter, pass null as-is
                    break;
                }

                default:
                    throw new NotSupportedException($"Collection parameter type {parameterType} not supported.");
            }
        }

        EmitCall(il, factory);
        il.Emit(OpCodes.Ret);
        return dynamicMethod;

        void LdOptionsProperty(string propertyName)
        {
            il.Emit(parameterTypes.Length == 1 ? OpCodes.Ldarg_0 : OpCodes.Ldarg_1);
            MethodInfo getter = optionsType.GetProperty(propertyName)!.GetMethod!;
            il.Emit(OpCodes.Call, getter);
        }
    }
}
