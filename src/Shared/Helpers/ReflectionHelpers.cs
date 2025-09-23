using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace PolyType.ReflectionProvider;

internal static class ReflectionHelpers
{
    private const string RequiresUnreferencedCodeMessage = "The method requires unreferenced code.";
    private const string RequiresDynamicCodeMessage = "The method requires dynamic code generation.";

    public static bool IsDynamicCodeSupported { get; } = IsDynamicCodeSupportedCore();
    private static bool IsDynamicCodeSupportedCore()
    {
#if NET
        return RuntimeFeature.IsDynamicCodeSupported;
#else
        if (RuntimeInformation.FrameworkDescription.StartsWith(".NET Framework", StringComparison.Ordinal))
        {
            return true; // .NET Framework supports dynamic code generation.
        }

        // Check for the presence of the property in .NET 6+.
        PropertyInfo? prop = Type.GetType("System.Runtime.CompilerServices.RuntimeFeature")?.GetProperty("IsDynamicCodeSupported", BindingFlags.Public | BindingFlags.Static);
        return prop is not null && (bool)prop.GetValue(null);
#endif
    }

    public static bool IsMonoRuntime { get; } = Type.GetType("Mono.Runtime") is not null;
    public static bool IsNetFramework { get; } = RuntimeInformation.FrameworkDescription.StartsWith(".NET Framework", StringComparison.Ordinal);

    public static bool IsNullabilityInfoContextSupported { get; } =
#if NET
        !AppContext.TryGetSwitch("System.Reflection.NullabilityInfoContext.IsSupported", out bool isSupported) || isSupported;
#else
        true; // The ns2.0 build uses a polyfill that always supports NRTs.
#endif

    public static bool IsNullableStruct(this Type type)
    {
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
    }

    public static bool IsNullable(this Type type)
    {
        return !type.IsValueType || type.IsNullableStruct();
    }

    public static bool IsIEnumerable(this Type type)
    {
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>);
    }

    public static void ResolveNullableAnnotation(this MemberInfo memberInfo, NullabilityInfoContext? ctx, out bool isGetterNonNullable, out bool isSetterNonNullable)
    {
        if (GetNullabilityInfo(memberInfo, ctx) is NullabilityInfo info)
        {
            isGetterNonNullable = info.ReadState is NullabilityState.NotNull;
            isSetterNonNullable = info.WriteState is NullabilityState.NotNull;
        }
        else
        {
            // The member type is a non-nullable struct.
            isGetterNonNullable = true;
            isSetterNonNullable = true;
        }
    }

#if !NET
    public static TDelegate CreateDelegate<TDelegate>(this MethodInfo methodInfo) where TDelegate : Delegate
        => (TDelegate)Delegate.CreateDelegate(typeof(TDelegate), methodInfo);
#endif

    public static bool IsNonNullableAnnotation(this ParameterInfo parameterInfo, NullabilityInfoContext? ctx)
    {
        if (GetNullabilityInfo(parameterInfo, ctx) is NullabilityInfo info)
        {
#if NET && !NET9_0_OR_GREATER
            // Workaround for https://github.com/dotnet/runtime/issues/92487
            // The fix has been incorporated into .NET 9 (and the polyfilled implementations in netfx).
            // Should be removed once .NET 8 support is dropped.
            if (parameterInfo.GetGenericParameterDefinition() is ParameterInfo genericParam &&
                genericParam.GetParameterType() is { IsGenericTypeParameter: true } typeParam)
            {
                // Step 1. Look for nullable annotations on the type parameter.
                if (GetNullableFlags(typeParam) is byte[] flags)
                {
                    return flags[0] == 1;
                }

                // Step 2. Look for nullable annotations on the generic method declaration.
                if (typeParam.DeclaringMethod != null && GetNullableContextFlag(typeParam.DeclaringMethod) is byte flag)
                {
                    return flag == 1;
                }

                // Step 3. Look for nullable annotations on the generic method declaration.
                if (GetNullableContextFlag(typeParam.DeclaringType!) is byte flag2)
                {
                    return flag2 == 1;
                }

                // Default to nullable.
                return false;

                static byte[]? GetNullableFlags(MemberInfo member)
                {
                    foreach (CustomAttributeData attr in member.GetCustomAttributesData())
                    {
                        Type attrType = attr.AttributeType;
                        if (attrType is { Name: "NullableAttribute", Namespace: "System.Runtime.CompilerServices" })
                        {
                            foreach (CustomAttributeTypedArgument ctorArg in attr.ConstructorArguments)
                            {
                                switch (ctorArg.Value)
                                {
                                    case byte flag:
                                        return [flag];
                                    case byte[] flags:
                                        return flags;
                                }
                            }
                        }
                    }

                    return null;
                }

                static byte? GetNullableContextFlag(MemberInfo member)
                {
                    foreach (CustomAttributeData attr in member.GetCustomAttributesData())
                    {
                        Type attrType = attr.AttributeType;
                        if (attrType is { Name: "NullableContextAttribute", Namespace: "System.Runtime.CompilerServices" })
                        {
                            foreach (CustomAttributeTypedArgument ctorArg in attr.ConstructorArguments)
                            {
                                if (ctorArg.Value is byte flag)
                                {
                                    return flag;
                                }
                            }
                        }
                    }

                    return null;
                }
            }
#endif
            return info.WriteState is NullabilityState.NotNull;
        }
        else
        {
            // The parameter type is a non-nullable struct.
            return true;
        }
    }

    private static NullabilityInfo? GetNullabilityInfo(ICustomAttributeProvider memberInfo, NullabilityInfoContext? context)
    {
        Debug.Assert(memberInfo is PropertyInfo or FieldInfo or ParameterInfo);

        if (context is not null)
        {
            switch (memberInfo)
            {
                case PropertyInfo prop when prop.PropertyType.IsNullable():
                    return context.Create(prop);

                case FieldInfo field when field.FieldType.IsNullable():
                    return context.Create(field);

                case ParameterInfo parameter when parameter.GetParameterType().IsNullable():
                    return context.Create(parameter);
            }
        }

        return null;
    }

    public static ParameterInfo GetGenericParameterDefinition(this ParameterInfo parameter)
    {
        if (parameter.Member is { DeclaringType.IsConstructedGenericType: true }
                             or MethodInfo { IsGenericMethod: true })
        {
            var genericMethod = (MethodBase)parameter.Member.GetGenericMemberDefinition();
            return genericMethod.GetParameters()[parameter.Position];
        }

        return parameter;
    }

    public static Type GetParameterType(this ParameterInfo parameter)
    {
        Type parameterType = parameter.ParameterType;
        if (parameterType.IsByRef)
        {
            parameterType = parameterType.GetElementType()!;
        }

        return parameterType;
    }

    public static Type GetEffectiveParameterType(this ParameterInfo type)
    {
        Type parameterType = type.ParameterType;
        return parameterType.IsByRef ? parameterType.GetElementType()! : parameterType;
    }

    public static Type? GetEffectiveReturnType(this MethodInfo methodInfo)
    {
        Type returnType = methodInfo.ReturnType;
        if (returnType == typeof(void) || returnType == typeof(ValueTask) || returnType == typeof(Task))
        {
            return null;
        }

        if (returnType.IsByRef)
        {
            return returnType.GetElementType();
        }

        if (returnType.IsGenericType)
        {
            Type genericTypeDefinition = returnType.GetGenericTypeDefinition();
            if (genericTypeDefinition == typeof(ValueTask<>) || genericTypeDefinition == typeof(Task<>))
            {
                return returnType.GetGenericArguments()[0];
            }
        }

        return returnType;
    }

    public static bool IsAsyncType(this Type type)
    {
        if (typeof(Task).IsAssignableFrom(type) || type == typeof(ValueTask))
        {
            return true;
        }

        if (type.IsGenericType)
        {
            Type genericTypeDefinition = type.GetGenericTypeDefinition();
            return genericTypeDefinition == typeof(ValueTask<>);
        }

        return false;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2075:'this' argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method. The return value of the source method does not have matching annotations.", Justification = "Looking up the generic member definition of the input.")]
    public static MemberInfo GetGenericMemberDefinition(this MemberInfo member)
    {
        if (member is Type type)
        {
            return type.IsConstructedGenericType ? type.GetGenericTypeDefinition() : type;
        }

        if (member.DeclaringType!.IsConstructedGenericType)
        {
            return member.DeclaringType.GetGenericTypeDefinition()
                .GetMember(member.Name, AllMemberFlags)
                .First(m => m.MetadataToken == member.MetadataToken);
        }

        if (member is MethodInfo { IsGenericMethod: true } method)
        {
            return method.GetGenericMethodDefinition();
        }

        return member;
    }

    public static bool CanBeGenericArgument(this Type type)
    {
        return !(type == typeof(void) || type.IsPointer || type.IsByRef || IsByRefLike(type) || type.ContainsGenericParameters);
    }

    public static bool IsEnumDefined<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
#if NET
        return Enum.IsDefined(value);
#else
        return Enum.IsDefined(typeof(TEnum), value);
#endif
    }

    private static bool IsByRefLike(Type type)
    {
#if NET
        return type.IsByRefLike;
#else
        if (!type.IsValueType)
        {
            return false;
        }

        foreach (var attributeData in type.GetCustomAttributesData())
        {
            if (attributeData.AttributeType.FullName == "System.Runtime.CompilerServices.IsByRefLikeAttribute")
            {
                return true;
            }
        }

        return false;
#endif
    }

    public static object? Invoke(this MethodBase methodBase, params object?[]? args)
    {
        DebugExt.Assert(methodBase is ConstructorInfo or MethodBase { IsStatic: true });
        return methodBase is ConstructorInfo ctor ? ctor.Invoke(args) : methodBase.Invoke(null, args);
    }

    public static bool IsMemoryType(this Type type, [NotNullWhen(true)] out Type? elementType, out bool isReadOnlyMemory)
    {
        if (type is { IsGenericType: true, IsValueType: true })
        {
            Type genericTypeDefinition = type.GetGenericTypeDefinition();
            if (genericTypeDefinition == typeof(ReadOnlyMemory<>))
            {
                elementType = type.GetGenericArguments()[0];
                isReadOnlyMemory = true;
                return true;
            }

            if (genericTypeDefinition == typeof(Memory<>))
            {
                elementType = type.GetGenericArguments()[0];
                isReadOnlyMemory = false;
                return true;
            }
        }

        elementType = null;
        isReadOnlyMemory = false;
        return false;
    }

    [RequiresUnreferencedCode(RequiresUnreferencedCodeMessage)]
    public static bool IsRecordType(this Type type)
    {
        return !type.IsValueType
            ? type.GetMethod("<Clone>$", BindingFlags.Public | BindingFlags.Instance) is not null
            : type.GetMethod("PrintMembers", BindingFlags.NonPublic | BindingFlags.Instance, null, [typeof(StringBuilder)], null) is { } method
                && method.ReturnType == typeof(bool)
                && method.GetCustomAttributesData().Any(attr => attr.AttributeType.Name == "CompilerGeneratedAttribute");
    }

    public static Type GetMemberType(this MemberInfo memberInfo)
    {
        DebugExt.Assert(memberInfo is FieldInfo or PropertyInfo);
        return memberInfo is FieldInfo f ? f.FieldType : ((PropertyInfo)memberInfo).PropertyType;
    }

    public static void ResolveAccessibility(this MemberInfo memberInfo, out bool isGetterPublic, out bool isSetterPublic)
    {
        DebugExt.Assert(memberInfo is FieldInfo or PropertyInfo);
        if (memberInfo is PropertyInfo propertyInfo)
        {
            isGetterPublic = propertyInfo.GetMethod?.IsPublic is true;
            isSetterPublic = propertyInfo.SetMethod?.IsPublic is true;
        }
        else
        {
            isGetterPublic = isSetterPublic = ((FieldInfo)memberInfo).IsPublic;
        }
    }

    [RequiresUnreferencedCode(RequiresUnreferencedCodeMessage)]
    [RequiresDynamicCode(RequiresDynamicCodeMessage)]
    public static IEnumerable<MethodInfo> GetCollectionBuilderAttributeMethods(this Type collectionType, Type elementType)
    {
        CustomAttributeData? attributeData = collectionType.CustomAttributes
            .FirstOrDefault(attr => attr.AttributeType.FullName == "System.Runtime.CompilerServices.CollectionBuilderAttribute");

        if (attributeData is null)
        {
            yield break;
        }

        Type builderType = (Type)attributeData.ConstructorArguments[0].Value!;
        string methodName = (string)attributeData.ConstructorArguments[1].Value!;

        if (builderType.IsGenericType)
        {
            yield break;
        }

        foreach (MethodInfo method in builderType.GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            if (method.Name != methodName)
            {
                continue;
            }

            if (method.IsGenericMethod)
            {
                if (method.GetGenericArguments().Length != 1)
                {
                    continue;
                }

                yield return method.MakeGenericMethod(elementType);
            }
            else
            {
                yield return method;
            }
        }
    }

    public static bool IsInitOnly(this MemberInfo memberInfo)
    {
        return memberInfo is PropertyInfo { SetMethod: { } setter } &&
            setter.ReturnParameter
                .GetRequiredCustomModifiers()
                .Any(static modifier => modifier.FullName == "System.Runtime.CompilerServices.IsExternalInit");
    }

    public static bool IsRequired(this MemberInfo memberInfo)
    {
        DebugExt.Assert(memberInfo is FieldInfo or PropertyInfo);
        return memberInfo.CustomAttributes.Any(static attr => attr.AttributeType.FullName == "System.Runtime.CompilerServices.RequiredMemberAttribute");
    }

    public static bool SetsRequiredMembers(this MethodBase ctorInfo)
    {
        return ctorInfo.CustomAttributes.Any(static attr => attr.AttributeType.FullName == "System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute");
    }

    public static Type MemberType(this MemberInfo memberInfo)
    {
        DebugExt.Assert(memberInfo is FieldInfo or PropertyInfo);
        return memberInfo is FieldInfo f ? f.FieldType : ((PropertyInfo)memberInfo).PropertyType;
    }

    [RequiresUnreferencedCode(RequiresUnreferencedCodeMessage)]
    public static IEnumerable<Type> GetAllInterfaces(this Type type)
    {
        if (type.IsInterface)
        {
            yield return type;
        }

        foreach (Type interfaceType in type.GetInterfaces())
        {
            yield return interfaceType;
        }
    }

    /// <summary>
    /// Resolves all members that are visible on the specified type,
    /// accounting for overrides and shadowing in the type hierarchy.
    /// </summary>
    /// <param name="type">The type to resolve members for.</param>
    /// <returns>The type's visible members.</returns>
    /// <remarks>
    /// Note that this method doesn't take accessibility modifiers into account.
    /// </remarks>
    [RequiresUnreferencedCode(RequiresUnreferencedCodeMessage)]
    internal static IEnumerable<MemberInfo> ResolveVisibleMembers(this Type type)
    {
        Dictionary<string, List<Type>> membersInScope = new(StringComparer.Ordinal);
        foreach (Type current in type.GetSortedTypeHierarchy())
        {
            foreach (MemberInfo member in current.GetMembers(AllMemberFlags))
            {
                // To account for overloads, method identifiers include the method name and parameter types but not the return type.
                string identifier = member is MethodInfo method ?
                    $"{method.Name}({string.Join(", ", method.GetParameters().Select(p => p.ParameterType.AssemblyQualifiedName))})" :
                    member.Name;

                if (membersInScope.TryGetValue(identifier, out List<Type>? declaringTypes))
                {
                    if (declaringTypes.Exists(current.IsAssignableFrom))
                    {
                        continue; // Member is overridden or shadowed by a more derived type in the hierarchy.
                    }

                    // Member forms diamond ambiguity, include in case there is disambiguation on attribute configs.
                    Debug.Assert(type.IsInterface);
                }
                else
                {
                    membersInScope[identifier] = declaringTypes = [];
                }

                declaringTypes.Add(current);
                yield return member;
            }
        }
    }

    public static bool IsExplicitInterfaceImplementation(this MethodInfo methodInfo)
    {
        DebugExt.Assert(!methodInfo.IsStatic);
        return methodInfo is { IsPrivate: true, IsVirtual: true } && methodInfo.Name.Contains('.');
    }

    public static bool IsExplicitInterfaceImplementation(this PropertyInfo propertyInfo)
    {
        return
            propertyInfo.GetMethod?.IsExplicitInterfaceImplementation() == true ||
            propertyInfo.SetMethod?.IsExplicitInterfaceImplementation() == true;
    }

    public static bool IsStatic(this PropertyInfo propertyInfo)
    {
        return (propertyInfo.GetMethod ?? propertyInfo.SetMethod)!.IsStatic;
    }

    [RequiresUnreferencedCode(RequiresUnreferencedCodeMessage)]
    public static PropertyInfo GetBaseDefinition(this PropertyInfo propertyInfo)
    {
        MethodInfo? getterOrSetter = propertyInfo.GetMethod ?? propertyInfo.SetMethod;
        DebugExt.Assert(getterOrSetter != null);
        if (getterOrSetter.IsVirtual)
        {
            MethodInfo baseDefinition = getterOrSetter.GetBaseDefinition();
            propertyInfo = baseDefinition.DeclaringType!.GetProperty(propertyInfo.Name, AllMemberFlags | BindingFlags.DeclaredOnly)!;
        }

        return propertyInfo;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2067", Justification = "Called conditionally on structs whose default ctor never gets trimmed.")]
    public static object? GetDefaultValue(Type type)
    {
        if (!type.IsValueType || type.IsNullableStruct())
        {
            return null;
        }
#if NET
        return RuntimeHelpers.GetUninitializedObject(type);
#else
        return System.Runtime.Serialization.FormatterServices.GetUninitializedObject(type);
#endif
    }

    public static bool TryGetDefaultValueNormalized(this ParameterInfo parameterInfo, out object? result)
    {
        if (!parameterInfo.HasDefaultValue)
        {
            result = null;
            return false;
        }

        Type parameterType = parameterInfo.ParameterType;
        result = parameterInfo.DefaultValue;

        if (result is null || result is DBNull)
        {
            result = GetDefaultValue(parameterInfo.ParameterType);
        }
        else if (parameterType.IsEnum)
        {
            result = Enum.ToObject(parameterType, result);
        }
        else if (Nullable.GetUnderlyingType(parameterType) is { IsEnum: true } underlyingType)
        {
            result = Enum.ToObject(underlyingType, result);
        }
        else if (parameterType == typeof(IntPtr))
        {
            result = checked((IntPtr)Convert.ToInt64(result, CultureInfo.InvariantCulture));
        }
        else if (parameterType == typeof(UIntPtr))
        {
            result = checked((UIntPtr)Convert.ToUInt64(result, CultureInfo.InvariantCulture));
        }

        return true;
    }

    [RequiresUnreferencedCode(RequiresUnreferencedCodeMessage)]
    public static ICollection<Type> GetSortedTypeHierarchy(this Type type)
    {
        if (!type.IsInterface)
        {
            var list = new List<Type>();
            for (Type? current = type; current != null; current = current.BaseType)
            {
                if (current == typeof(object) || current == typeof(ValueType))
                {
                    break;
                }

                list.Add(current);
            }

            return list;
        }
        else
        {
            // Interface hierarchies support multiple inheritance.
            // For consistency with class hierarchy resolution order,
            // sort topologically from most derived to least derived.
            return CommonHelpers.TraverseGraphWithTopologicalSort(type, static t => t.GetInterfaces());
        }
    }

    public static bool IsTupleType(this Type type)
    {
        if (type is not { Namespace: "System", Name: string name })
        {
            return false;
        }

        return name.StartsWith("ValueTuple", StringComparison.Ordinal) ||
            name.StartsWith("Tuple", StringComparison.Ordinal);
    }

    public static bool IsValueTupleType(this Type type)
        => type.IsTupleType() && type.IsValueType;

    public static bool IsNestedTupleRepresentation(this Type type)
    {
        if (!type.IsTupleType())
        {
            return false;
        }

        Type[] genericArguments = type.GetGenericArguments();
        return genericArguments.Length == 8 &&
            genericArguments[7].IsValueType == type.IsValueType &&
            genericArguments[7].IsTupleType();
    }

    [RequiresUnreferencedCode(RequiresUnreferencedCodeMessage)]
    [RequiresDynamicCode(RequiresDynamicCodeMessage)]
    public static Type CreateValueTupleType(Type[] elementTypes)
    {
        return elementTypes.Length switch
        {
            0 => typeof(ValueTuple),
            1 => typeof(ValueTuple<>).MakeGenericType(elementTypes),
            2 => typeof(ValueTuple<,>).MakeGenericType(elementTypes),
            3 => typeof(ValueTuple<,,>).MakeGenericType(elementTypes),
            4 => typeof(ValueTuple<,,,>).MakeGenericType(elementTypes),
            5 => typeof(ValueTuple<,,,,>).MakeGenericType(elementTypes),
            6 => typeof(ValueTuple<,,,,,>).MakeGenericType(elementTypes),
            7 => typeof(ValueTuple<,,,,,,>).MakeGenericType(elementTypes),
            _ => typeof(ValueTuple<,,,,,,,>).MakeGenericType([.. elementTypes.Take(7), CreateValueTupleType(elementTypes.Skip(7).ToArray())]),
        };
    }

    [RequiresUnreferencedCode(RequiresUnreferencedCodeMessage)]
    public static IEnumerable<(string LogicalName, MemberInfo Member, MemberInfo[]? ParentMembers)> EnumerateTupleMemberPaths(Type tupleType)
    {
        // Walks the nested tuple representation, returning every element field and the parent "Rest" fields needed to access the value.
        DebugExt.Assert(tupleType.IsTupleType());
        List<MemberInfo>? nestedMembers = null;
        bool hasNestedTuple;
        int i = 0;

        do
        {
            MemberInfo[]? parentMembers = tupleType.IsValueType
                ? nestedMembers?.OfType<FieldInfo>().ToArray()
                : nestedMembers?.OfType<PropertyInfo>().ToArray();

            MemberInfo[] elements = tupleType.IsValueType
                ? tupleType.GetFields(BindingFlags.Instance | BindingFlags.Public)
                : tupleType.GetProperties(BindingFlags.Instance | BindingFlags.Public);

            hasNestedTuple = false;

            foreach (MemberInfo element in elements.OrderBy(e => e.Name))
            {
                if (element.Name is "Rest")
                {
                    Type memberType = element.MemberType();
                    if (memberType.IsTupleType())
                    {
                        (nestedMembers ??= []).Add(element);
                        tupleType = memberType;
                        hasNestedTuple = true;
                    }
                    else
                    {
                        yield return ("Rest", element, parentMembers);
                    }
                }
                else
                {
                    yield return ($"Item{++i}", element, parentMembers);
                }
            }
        }
        while (hasNestedTuple);
    }

    private const BindingFlags AllMemberFlags =
        BindingFlags.Static | BindingFlags.Instance |
        BindingFlags.Public | BindingFlags.NonPublic;
}
