using PolyType.Abstractions;
using PolyType.ReflectionProvider.MemberAccessors;
using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace PolyType.ReflectionProvider;

/// <summary>
/// Provides a <see cref="ITypeShapeProvider"/> implementation that uses reflection.
/// </summary>
[RequiresUnreferencedCode(RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(RequiresDynamicCodeMessage)]
public class ReflectionTypeShapeProvider : ITypeShapeProvider
{
    internal const string RequiresUnreferencedCodeMessage = "PolyType Reflection provider requires unreferenced code.";
    internal const string RequiresDynamicCodeMessage = "PolyType Reflection provider requires dynamic code.";

    private static readonly ConcurrentDictionary<ReflectionTypeShapeProviderOptions, ReflectionTypeShapeProvider> s_providers = new();

    /// <summary>
    /// Gets the default provider instance using configuration supported by the current platform.
    /// </summary>
    public static ReflectionTypeShapeProvider Default { get; } = new ReflectionTypeShapeProvider(ReflectionTypeShapeProviderOptions.Default);

    /// <summary>
    /// Initializes a new instance of the <see cref="ReflectionTypeShapeProvider"/> class.
    /// </summary>
    /// <param name="options">The options governing the shape provider instance.</param>
    /// <returns>A <see cref="ReflectionTypeShapeProviderOptions"/> corresponding to the specified options.</returns>
    public static ReflectionTypeShapeProvider Create(ReflectionTypeShapeProviderOptions options)
    {
        Throw.IfNull(options);

        if (options == ReflectionTypeShapeProviderOptions.Default)
        {
            return Default;
        }

        return s_providers.GetOrAdd(options, _ => new ReflectionTypeShapeProvider(options));
    }

    private readonly ConditionalWeakTable<Type, ITypeShape> _cache = new();
    private readonly ConditionalWeakTable<Type, ITypeShape>.CreateValueCallback _typeShapeFactory;
    private readonly TypeShapeExtensionAttribute[] _typeShapeExtensions;

    private ReflectionTypeShapeProvider(ReflectionTypeShapeProviderOptions options)
    {
        if (options.UseReflectionEmit && !ReflectionHelpers.IsDynamicCodeSupported)
        {
            throw new PlatformNotSupportedException("Dynamic code generation is not supported on the current platform.");
        }

        Options = options;
        MemberAccessor = options.UseReflectionEmit
            ? new ReflectionEmitMemberAccessor()
            : new ReflectionMemberAccessor();

        _typeShapeExtensions = options.TypeShapeExtensionAssemblies
            .SelectMany(assembly => assembly.GetCustomAttributes<TypeShapeExtensionAttribute>())
            .ToArray();

        _typeShapeFactory = type => CreateTypeShapeCore(type);
    }

    /// <summary>
    /// Gets the configuration used by the provider.
    /// </summary>
    public ReflectionTypeShapeProviderOptions Options { get; }

    internal IReflectionMemberAccessor MemberAccessor { get; }

    /// <summary>
    /// Gets a <see cref="ITypeShape{T}"/> instance corresponding to the supplied type.
    /// </summary>
    /// <typeparam name="T">The type for which a shape is requested.</typeparam>
    /// <returns>
    /// A <see cref="ITypeShape{T}"/> instance corresponding to the current type.
    /// </returns>
    public ITypeShape<T> GetTypeShape<T>() => (ITypeShape<T>)GetTypeShape(typeof(T));

    /// <summary>
    /// Gets a <see cref="ITypeShape"/> instance corresponding to the supplied type.
    /// </summary>
    /// <param name="type">The type for which a shape is requested.</param>
    /// <returns>
    /// A <see cref="ITypeShape"/> instance corresponding to the current type.
    /// </returns>
    /// <exception cref="ArgumentNullException">The <paramref name="type"/> argument is null.</exception>
    /// <exception cref="ArgumentException">The <paramref name="type"/> cannot be a generic argument.</exception>
    public ITypeShape GetTypeShape(Type type)
    {
        Throw.IfNull(type);
        return _cache.GetValue(type, _typeShapeFactory);
    }

    internal ITypeShape CreateTypeShapeCore(Type type, bool allowUnionShapes = true)
    {
        DebugExt.Assert(type != null);

        if (!type.CanBeGenericArgument())
        {
            throw new ArgumentException("Type cannot be a generic parameter", nameof(type));
        }

        ReflectionTypeShapeOptions options = ResolveTypeShapeOptions(type);
        return DetermineTypeKind(type, allowUnionShapes, options, out FSharpUnionInfo? fSharpUnionInfo, out FSharpFuncInfo? fSharpFuncInfo) switch
        {
            TypeShapeKind.Enumerable => CreateEnumerableShape(type, options),
            TypeShapeKind.Dictionary => CreateDictionaryShape(type, options),
            TypeShapeKind.Enum => CreateEnumShape(type, options),
            TypeShapeKind.Optional => CreateOptionalShape(type, fSharpUnionInfo, options),
            TypeShapeKind.Object => CreateObjectShape(type, disableMemberResolution: false, options),
            TypeShapeKind.Surrogate => CreateSurrogateShape(type, options),
            TypeShapeKind.Union => CreateUnionTypeShape(type, fSharpUnionInfo, options),
            TypeShapeKind.Function => CreateFunctionTypeShape(type, fSharpFuncInfo, options),
            TypeShapeKind.None or _ => CreateObjectShape(type, disableMemberResolution: true, options),
        };
    }

    private ITypeShape CreateObjectShape(Type type, bool disableMemberResolution, ReflectionTypeShapeOptions options)
    {
        if (FSharpReflectionHelpers.IsFSharpUnitType(type))
        {
            Type unitTypeShapeTy = typeof(FSharpUnitTypeShape<>).MakeGenericType(type);
            return (ITypeShape)Activator.CreateInstance(unitTypeShapeTy, this, options)!;
        }

        Type objectShapeTy = typeof(DefaultReflectionObjectTypeShape<>).MakeGenericType(type);
        return (ITypeShape)Activator.CreateInstance(objectShapeTy, this, disableMemberResolution, options)!;
    }

    private ITypeShape CreateSurrogateShape(Type type, ReflectionTypeShapeOptions options)
    {
        DebugExt.Assert(options.Marshaler != null);

        Type marshalerType = options.Marshaler;
        if (marshalerType.IsGenericTypeDefinition)
        {
            // Generic marshalers are applied the type parameters from the declaring type.
            marshalerType = marshalerType.MakeGenericType(type.GetGenericArguments());
        }

        // First check that the marshaler implements exactly one IMarshaler<,> for the source type.
        Type? matchingSurrogate = null;
        foreach (Type interfaceType in marshalerType.GetAllInterfaces())
        {
            if (interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(IMarshaler<,>))
            {
                Type[] genericArgs = interfaceType.GetGenericArguments();
                if (genericArgs[0] == type)
                {
                    if (matchingSurrogate != null)
                    {
                        throw new InvalidOperationException($"The type '{marshalerType}' defines conflicting surrogate marshalers from type '{type}'.");
                    }

                    matchingSurrogate = genericArgs[1];
                }
            }
        }

        if (matchingSurrogate is null)
        {
            throw new InvalidOperationException($"The type '{marshalerType}' does not define any surrogate marshalers from type '{type}'.");
        }

        object bijection = Activator.CreateInstance(marshalerType)!;
        Type surrogateTypeShapeTy = typeof(ReflectionSurrogateTypeShape<,>).MakeGenericType(type, matchingSurrogate);
        return (ITypeShape)Activator.CreateInstance(surrogateTypeShapeTy, bijection, this, options)!;
    }

    private IEnumerableTypeShape CreateEnumerableShape(Type type, ReflectionTypeShapeOptions options)
    {
        if (type.IsArray)
        {
            Type elementType = type.GetElementType()!;
            int rank = type.GetArrayRank();

            if (rank == 1)
            {
                Type enumerableTypeTy = typeof(ReflectionArrayTypeShape<>).MakeGenericType(elementType);
                return (IEnumerableTypeShape)Activator.CreateInstance(enumerableTypeTy, this, options)!;
            }
            else
            {
                Type enumerableTypeTy = typeof(MultiDimensionalArrayTypeShape<,>).MakeGenericType(type, elementType);
                return (IEnumerableTypeShape)Activator.CreateInstance(enumerableTypeTy, this, rank, options)!;
            }
        }

        if (TryGetInlineArrayElementType(type, out Type? inlineElementType, out int length))
        {
            Type enumerableTypeTy = typeof(ReflectionInlineArrayTypeShape<,>).MakeGenericType(type, inlineElementType);
            return (IEnumerableTypeShape)Activator.CreateInstance(enumerableTypeTy, length, this, options)!;
        }

        foreach (Type interfaceTy in type.GetAllInterfaces().Where(t => t.IsGenericType).OrderByDescending(t => t.Name))
        {
            // Sort by name so that IEnumerable takes precedence over IAsyncEnumerable
            Type genericInterfaceTypeDef = interfaceTy.GetGenericTypeDefinition();

            if (genericInterfaceTypeDef == typeof(IEnumerable<>))
            {
                Type elementType = interfaceTy.GetGenericArguments()[0];
                Type enumerableTypeTy = typeof(ReflectionEnumerableTypeOfTShape<,>).MakeGenericType(type, elementType);
                return (IEnumerableTypeShape)Activator.CreateInstance(enumerableTypeTy, this, options)!;
            }

            if (genericInterfaceTypeDef.FullName == "System.Collections.Generic.IAsyncEnumerable`1")
            {
                Type elementType = interfaceTy.GetGenericArguments()[0];
                Type enumerableTypeTy = typeof(ReflectionAsyncEnumerableShape<,>).MakeGenericType(type, elementType);
                return (IEnumerableTypeShape)Activator.CreateInstance(enumerableTypeTy, this, options)!;
            }
        }

        if (type.IsMemoryType(out Type? memoryElementType, out bool isReadOnlyMemory))
        {
            Type shapeType = isReadOnlyMemory ? typeof(ReadOnlyMemoryTypeShape<>) : typeof(MemoryTypeShape<>);
            Type enumerableTypeTy = shapeType.MakeGenericType(memoryElementType);
            return (IEnumerableTypeShape)Activator.CreateInstance(enumerableTypeTy, this, options)!;
        }

        Type enumerableType = typeof(ReflectionNonGenericEnumerableTypeShape<>).MakeGenericType(type);
        return (IEnumerableTypeShape)Activator.CreateInstance(enumerableType, this, options)!;
    }

    private IDictionaryTypeShape CreateDictionaryShape(Type type, ReflectionTypeShapeOptions options)
    {
        Type? dictionaryTypeTy = null;

        foreach (Type interfaceTy in type.GetAllInterfaces())
        {
            if (interfaceTy.IsGenericType)
            {
                Type genericInterfaceTy = interfaceTy.GetGenericTypeDefinition();
                Type[] genericArgs = interfaceTy.GetGenericArguments();

                if (genericInterfaceTy == typeof(IDictionary<,>))
                {
                    dictionaryTypeTy = typeof(ReflectionDictionaryOfTShape<,,>)
                        .MakeGenericType(type, genericArgs[0], genericArgs[1]);
                }
                else if (genericInterfaceTy == typeof(IReadOnlyDictionary<,>))
                {
                    dictionaryTypeTy = typeof(ReflectionReadOnlyDictionaryShape<,,>)
                        .MakeGenericType(type, genericArgs[0], genericArgs[1]);

                    break; // IReadOnlyDictionary takes precedence over IDictionary
                }
            }
        }

        if (dictionaryTypeTy is null)
        {
            Debug.Assert(typeof(IDictionary).IsAssignableFrom(type));
            dictionaryTypeTy = typeof(ReflectionNonGenericDictionaryShape<>).MakeGenericType(type);
        }

        return (IDictionaryTypeShape)Activator.CreateInstance(dictionaryTypeTy, this, options)!;
    }

    private IEnumTypeShape CreateEnumShape(Type enumType, ReflectionTypeShapeOptions options)
    {
        Debug.Assert(enumType.IsEnum);
        Type enumTypeTy = typeof(ReflectionEnumTypeShape<,>).MakeGenericType(enumType, Enum.GetUnderlyingType(enumType));
        return (IEnumTypeShape)Activator.CreateInstance(enumTypeTy, this, options)!;
    }

    private IOptionalTypeShape CreateOptionalShape(Type optionalType, FSharpUnionInfo? fsharpUnionInfo, ReflectionTypeShapeOptions options)
    {
        if (fsharpUnionInfo is not null)
        {
            Debug.Assert(fsharpUnionInfo.IsOptional && optionalType.IsGenericType);
            Type fsharpOptionalType = typeof(FSharpOptionTypeShape<,>).MakeGenericType(optionalType, optionalType.GetGenericArguments()[0]);
            return (IOptionalTypeShape)Activator.CreateInstance(fsharpOptionalType, fsharpUnionInfo, this, options)!;
        }

        Debug.Assert(optionalType.IsNullableStruct());
        Type nullableTypeTy = typeof(ReflectionNullableTypeShape<>).MakeGenericType(optionalType.GetGenericArguments());
        return (IOptionalTypeShape)Activator.CreateInstance(nullableTypeTy, this, options)!;
    }

    private IUnionTypeShape CreateUnionTypeShape(Type unionType, FSharpUnionInfo? fSharpUnionInfo, ReflectionTypeShapeOptions options)
    {
        if (fSharpUnionInfo is not null)
        {
            Type fsharpUnionTypeTy = typeof(FSharpUnionTypeShape<>).MakeGenericType(unionType);
            return (IUnionTypeShape)Activator.CreateInstance(fsharpUnionTypeTy, fSharpUnionInfo, this, options)!;
        }

        List<DerivedTypeShapeAttribute> derivedTypeAttributes = unionType.GetCustomAttributes<DerivedTypeShapeAttribute>().ToList();
        if (unionType.GetCustomAttribute<DataContractAttribute>() is { })
        {
            var mappedKnownTypeAttributes = unionType.GetCustomAttributes<KnownTypeAttribute>()
                .Select(attr =>
                {
                    if (attr.Type is null)
                    {
                        throw new NotSupportedException("KnownTypeAttribute annotations using methods are not supported.");
                    }

                    return new DerivedTypeShapeAttribute(attr.Type);
                });

            derivedTypeAttributes.AddRange(mappedKnownTypeAttributes);
        }

        HashSet<Type> types = new();
        HashSet<string> names = new(StringComparer.Ordinal);
        HashSet<int> tags = new();
        List<DerivedTypeInfo> derivedTypeInfos = [];
        foreach (DerivedTypeShapeAttribute derivedTypeAttribute in derivedTypeAttributes)
        {
            Type derivedType = derivedTypeAttribute.Type;

            if (derivedType.IsGenericTypeDefinition)
            {
                try
                {
                    // Accept generic derived types provided we can apply the type parameters for the base type.
                    Type derivedWithBaseTypeParams = derivedType.MakeGenericType(unionType.GetGenericArguments());
                    if (!unionType.IsAssignableFrom(derivedWithBaseTypeParams))
                    {
                        throw new InvalidOperationException();
                    }

                    derivedType = derivedWithBaseTypeParams;
                }
                catch
                {
                    throw new InvalidOperationException($"The declared derived type '{derivedType}' introduces unsupported type parameters over '{unionType}'.");
                }
            }
            else if (!unionType.IsAssignableFrom(derivedType))
            {
                throw new InvalidOperationException($"The declared derived type '{derivedType}' is not a valid subtype of '{unionType}'.");
            }

            string name = derivedTypeAttribute.Name;
            int index = derivedTypeInfos.Count;
            int tag = derivedTypeAttribute.Tag;
            bool isTagSpecified = true;
            if (tag < 0)
            {
                tag = index;
                isTagSpecified = false;
            }

            if (!types.Add(derivedType))
            {
                throw new InvalidOperationException($"Polymorphic type '{unionType}' uses duplicate assignments for the derived type '{derivedTypeAttribute.Type}'.");
            }

            if (!tags.Add(tag))
            {
                throw new InvalidOperationException($"Polymorphic type '{unionType}' uses duplicate assignments for the tag {tag}.");
            }

            if (!names.Add(name))
            {
                throw new InvalidOperationException($"Polymorphic type '{unionType}' uses duplicate assignments for the name '{name}'.");
            }

            derivedTypeInfos.Add(new(derivedType, name, tag, index, isTagSpecified));
        }

        Type unionTypeTy = typeof(ReflectionUnionTypeShape<>).MakeGenericType(unionType);
        return (IUnionTypeShape)Activator.CreateInstance(unionTypeTy, derivedTypeInfos.ToArray(), this, options)!;
    }

    private IFunctionTypeShape CreateFunctionTypeShape(Type functionType, FSharpFuncInfo? fSharpFuncInfo, ReflectionTypeShapeOptions options)
    {
        if (fSharpFuncInfo is not null)
        {
            Type fsharpArgumentStateType = MemberAccessor.CreateConstructorArgumentStateType(fSharpFuncInfo);
            Type fsharpFunctionShapeTy = typeof(FSharpFunctionTypeShape<,,>).MakeGenericType(functionType, fsharpArgumentStateType, fSharpFuncInfo.EffectiveReturnType);
            return (IFunctionTypeShape)Activator.CreateInstance(fsharpFunctionShapeTy, fSharpFuncInfo, options, this)!;
        }

        DebugExt.Assert(typeof(Delegate).IsAssignableFrom(functionType));
        MethodInfo invokeMethod = functionType.GetMethod("Invoke")!;
        MethodShapeInfo methodShapeInfo = CreateMethodShapeInfo(invokeMethod, shapeAttribute: null, nullabilityCtx: CreateNullabilityInfoContext());
        Type argumentStateType = MemberAccessor.CreateConstructorArgumentStateType(methodShapeInfo);
        Type functionShapeTy = typeof(ReflectionDelegateTypeShape<,,>).MakeGenericType(functionType, argumentStateType, methodShapeInfo.ReturnType);
        return (IFunctionTypeShape)Activator.CreateInstance(functionShapeTy, methodShapeInfo, this, options)!;
    }

    private ReflectionTypeShapeOptions ResolveTypeShapeOptions(Type type)
    {
        Type? genericTypeDef = type.IsGenericType ? type.GetGenericTypeDefinition() : null;
        TypeShapeAttribute? typeShapeAttr = type.GetCustomAttribute<TypeShapeAttribute>();
        TypeShapeKind? requestedKind = typeShapeAttr?.GetRequestedKind();
        MethodShapeFlags? methodFlags = typeShapeAttr?.GetRequestedIncludeMethods();
        Type? marshaler = typeShapeAttr?.Marshaler;

        foreach (TypeShapeExtensionAttribute extensionAttr in _typeShapeExtensions)
        {
            if (extensionAttr.Target == type || extensionAttr.Target == genericTypeDef)
            {
                if (extensionAttr.Marshaler is not null)
                {
                    if (marshaler is not null && marshaler != extensionAttr.Marshaler)
                    {
                        throw new InvalidOperationException($"Conflicting marshalers for '{type}': '{marshaler}' vs '{extensionAttr.Marshaler}'.");
                    }

                    marshaler = extensionAttr.Marshaler;
                }

                if (extensionAttr.GetRequestedIncludeMethods() is { } includeMethods)
                {
                    if (methodFlags is not null && methodFlags != includeMethods)
                    {
                        throw new InvalidOperationException($"Conflicting method inclusion flags for '{type}': {methodFlags} vs {includeMethods}.");
                    }

                    methodFlags = includeMethods;
                }

                if (extensionAttr.GetRequestedKind() is { } extensionRequestedKind)
                {
                    if (requestedKind is not null && extensionRequestedKind != requestedKind)
                    {
                        throw new InvalidOperationException($"Conflicting requested shape kinds for '{type}': {requestedKind} vs {extensionRequestedKind}.");
                    }

                    requestedKind = extensionRequestedKind;
                }
            }
        }

        return new ReflectionTypeShapeOptions
        {
            RequestedKind = requestedKind,
            Marshaler = marshaler,
            IncludeMethods = methodFlags ?? MethodShapeFlags.None,
        };
    }

    private static TypeShapeKind DetermineTypeKind(Type type, bool allowUnionShapes, ReflectionTypeShapeOptions typeShapeOptions, out FSharpUnionInfo? fsharpUnionInfo, out FSharpFuncInfo? fSharpFuncInfo)
    {
        TypeShapeKind builtInKind = DetermineBuiltInTypeKind(type, allowUnionShapes, typeShapeOptions, out fsharpUnionInfo, out fSharpFuncInfo);

        if (typeShapeOptions.RequestedKind is TypeShapeKind requestedKind && requestedKind != builtInKind)
        {
            bool isCustomKindSupported = requestedKind switch
            {
                TypeShapeKind.Enum or TypeShapeKind.Optional or TypeShapeKind.Surrogate or TypeShapeKind.Union or TypeShapeKind.Function or TypeShapeKind.Dictionary => false,
                TypeShapeKind.Enumerable => builtInKind is TypeShapeKind.Dictionary,
                TypeShapeKind.Object or TypeShapeKind.None or _ => true,
            };

            if (!isCustomKindSupported)
            {
                throw new NotSupportedException($"TypeShapeKind '{requestedKind}' is not supported for type '{type}'.");
            }

            return requestedKind;
        }

        return builtInKind;
    }

    private static TypeShapeKind DetermineBuiltInTypeKind(
        Type type,
        bool allowUnionShapes,
        ReflectionTypeShapeOptions? options,
        out FSharpUnionInfo? fsharpUnionInfo,
        out FSharpFuncInfo? fSharpFuncInfo)
    {
        fsharpUnionInfo = null;
        fSharpFuncInfo = null;

        if (options?.Marshaler is not null)
        {
            return TypeShapeKind.Surrogate;
        }

        if (type.IsEnum)
        {
            return TypeShapeKind.Enum;
        }

        if (Nullable.GetUnderlyingType(type) is not null)
        {
            return TypeShapeKind.Optional;
        }

        if (typeof(Delegate).IsAssignableFrom(type))
        {
            return TypeShapeKind.Function;
        }

        if (FSharpReflectionHelpers.TryResolveFSharpUnionMetadata(type, out fsharpUnionInfo))
        {
            return fsharpUnionInfo.IsOptional ? TypeShapeKind.Optional : TypeShapeKind.Union;
        }

        if (FSharpReflectionHelpers.TryResolveFSharpFuncMetadata(type, out fSharpFuncInfo))
        {
            return TypeShapeKind.Function;
        }

        if (allowUnionShapes)
        {
            var customAttributeData = type.GetCustomAttributesData();
            if (customAttributeData.Any(attrData => attrData.AttributeType == typeof(DerivedTypeShapeAttribute)))
            {
                return TypeShapeKind.Union;
            }

            if (customAttributeData.Any(attrData => attrData.AttributeType == typeof(DataContractAttribute) &&
                customAttributeData.Any(attrData => attrData.AttributeType == typeof(KnownTypeAttribute))))
            {
                return TypeShapeKind.Union;
            }
        }

        if (typeof(IDictionary).IsAssignableFrom(type))
        {
            return TypeShapeKind.Dictionary;
        }

        bool foundAsyncEnumerable = false;
        foreach (Type interfaceTy in type.GetAllInterfaces())
        {
            if (interfaceTy.IsGenericType)
            {
                Type genericInterfaceTy = interfaceTy.GetGenericTypeDefinition();
                if (genericInterfaceTy == typeof(IDictionary<,>) ||
                    genericInterfaceTy == typeof(IReadOnlyDictionary<,>))
                {
                    return TypeShapeKind.Dictionary;
                }

                if (genericInterfaceTy.FullName == "System.Collections.Generic.IAsyncEnumerable`1")
                {
                    foundAsyncEnumerable = true;
                }
            }
        }

        if (foundAsyncEnumerable || (typeof(IEnumerable).IsAssignableFrom(type) && type != typeof(string)))
        {
            return TypeShapeKind.Enumerable;
        }

        if (type.IsMemoryType(out _, out _))
        {
            // Memory<T> or ReadOnlyMemory<T>
            return TypeShapeKind.Enumerable;
        }

        if (TryGetInlineArrayElementType(type, out _, out _))
        {
            return TypeShapeKind.Enumerable;
        }

        return TypeShapeKind.Object;
    }

    internal IPropertyShape CreateProperty(IObjectTypeShape declaringType, PropertyShapeInfo propertyShapeInfo, int position)
    {
        Type memberType = propertyShapeInfo.BaseMemberInfo.GetMemberType();
        Type reflectionPropertyType = typeof(ReflectionPropertyShape<,>).MakeGenericType(propertyShapeInfo.DeclaringType, memberType);
        return (IPropertyShape)Activator.CreateInstance(reflectionPropertyType, this, declaringType, propertyShapeInfo, position)!;
    }

    internal IMethodShape CreateMethod(ITypeShape declaringType, MethodShapeInfo methodShapeInfo)
    {
        Type argumentStateType = MemberAccessor.CreateConstructorArgumentStateType(methodShapeInfo);
        Type reflectionMethodShapeType = typeof(ReflectionMethodShape<,,>).MakeGenericType(declaringType.Type, argumentStateType, methodShapeInfo.ReturnType);
        return (IMethodShape)Activator.CreateInstance(reflectionMethodShapeType, methodShapeInfo, this)!;
    }

    internal IEventShape CreateEvent(ITypeShape declaringType, EventInfo eventInfo, string name)
    {
        Type eventShapeTy = typeof(ReflectionEventShape<,>).MakeGenericType(declaringType.Type, eventInfo.EventHandlerType!);
        return (IEventShape)Activator.CreateInstance(eventShapeTy, eventInfo, name, this)!;
    }

    internal IConstructorShape CreateConstructor(IObjectTypeShape declaringType, IMethodShapeInfo ctorInfo)
    {
        Type argumentStateType = MemberAccessor.CreateConstructorArgumentStateType(ctorInfo);
        Type reflectionConstructorType = typeof(ReflectionConstructorShape<,>).MakeGenericType(ctorInfo.ReturnType, argumentStateType);
        return (IConstructorShape)Activator.CreateInstance(reflectionConstructorType, this, declaringType, ctorInfo)!;
    }

    internal IParameterShape CreateParameter(Type constructorArgumentState, IMethodShapeInfo ctorInfo, int position)
    {
        IParameterShapeInfo parameterInfo = ctorInfo.Parameters[position];
        Type constructorParameterType = typeof(ReflectionParameterShape<,>).MakeGenericType(constructorArgumentState, parameterInfo.Type);
        return (IParameterShape)Activator.CreateInstance(constructorParameterType, this, ctorInfo, parameterInfo, position)!;
    }

    internal IUnionCaseShape CreateUnionCaseShape(IUnionTypeShape unionTypeShape, DerivedTypeInfo derivedTypeInfo)
    {
        Type unionCaseType = typeof(ReflectionUnionCaseShape<,>).MakeGenericType(derivedTypeInfo.Type, unionTypeShape.Type);
        return (IUnionCaseShape)Activator.CreateInstance(unionCaseType, unionTypeShape, derivedTypeInfo, this)!;
    }

    internal static IMethodShapeInfo CreateTupleConstructorShapeInfo(Type tupleType)
    {
        Debug.Assert(tupleType.IsTupleType());

        if (tupleType == typeof(ValueTuple))
        {
            return new MethodShapeInfo(tupleType, method: null, parameters: []);
        }

        if (!tupleType.IsNestedTupleRepresentation())
        {
            // Treat non-nested tuples as regular types.
            ConstructorInfo ctorInfo = tupleType.GetConstructors()[0];
            MethodParameterShapeInfo[] parameters = ctorInfo
                .GetParameters()
                .Select((p, i) => new MethodParameterShapeInfo(p, isNonNullable: false, logicalName: $"Item{i + 1}"))
                .ToArray();

            return new MethodShapeInfo(tupleType, ctorInfo, parameters);
        }

        return CreateNestedTupleCtorInfo(tupleType, offset: 0);

        static TupleConstructorShapeInfo CreateNestedTupleCtorInfo(Type tupleType, int offset)
        {
            Debug.Assert(tupleType.IsTupleType());
            ConstructorInfo ctorInfo = tupleType.GetConstructors()[0];
            ParameterInfo[] parameters = ctorInfo.GetParameters();
            MethodParameterShapeInfo[] ctorParameterInfo;
            TupleConstructorShapeInfo? nestedCtor;

            if (parameters.Length == 8 && parameters[7].ParameterType.IsTupleType())
            {
                ctorParameterInfo = MapParameterInfo(parameters.Take(7));
                nestedCtor = CreateNestedTupleCtorInfo(parameters[7].ParameterType, offset);
            }
            else
            {
                ctorParameterInfo = MapParameterInfo(parameters);
                nestedCtor = null;
            }

            return new TupleConstructorShapeInfo(tupleType, ctorInfo, ctorParameterInfo, nestedCtor);

            MethodParameterShapeInfo[] MapParameterInfo(IEnumerable<ParameterInfo> parameters)
                => parameters.Select(p => new MethodParameterShapeInfo(p, isNonNullable: false, logicalName: $"Item{++offset}")).ToArray();
        }
    }

    internal CollectionConstructorInfo? ResolveBestCollectionCtor<TElement, TKey>(
        Type collectionType,
        IEnumerable<MethodBase> candidates,
        MethodInfo? addMethod = null,
        MethodInfo? setMethod = null,
        MethodInfo? tryAddMethod = null,
        MethodInfo? containsKeyMethod = null,
        DictionaryInsertionMode insertionMode = DictionaryInsertionMode.None,
        Type? correspondingDictionaryType = null,
        Type? correspondingTupleEnumerableType = null)
    {
        bool isMutable = addMethod is not null || tryAddMethod is not null || setMethod is not null;
        return candidates
            .Select(method =>
            {
                var signature = ClassifyCollectionConstructor<TElement, TKey>(
                    collectionType,
                    method,
                    allowMutableCtor: isMutable,
                    out CollectionConstructorParameter? valuesParam,
                    out CollectionConstructorParameter? comparerParam,
                    correspondingDictionaryType,
                    correspondingTupleEnumerableType);

                return (Factory: method, Signature: signature, ValuesParam: valuesParam, ComparerParam: comparerParam);
            })
            .Where(result => result.Signature is not null)
            // Pick the best constructor based on the following criteria:
            // 1. Prefer constructors with a comparer parameter.
            // 2. Prefer constructors with a values parameter if not using an add method.
            // 3. Prefer constructors with a larger arity.
            // 4. Prefer constructors with a more efficient kind (Mutable > Span > Enumerable).
            .OrderBy(result =>
                (RankComparer(result.ComparerParam),
                 RankStrategy(result.ValuesParam),
                 -result.Signature!.Length,
                 RankValuePerformance(result.ValuesParam, result.Factory, result.Signature)))
            .Select(result => (MethodCollectionConstructorInfo)(isMutable && result.ValuesParam is null
                ? new MutableCollectionConstructorInfo(
                    result.Factory!,
                    result.Signature!,
                    addMethod,
                    setMethod,
                    tryAddMethod,
                    containsKeyMethod,
                    insertionMode,
                    GetComparerOptions(result.ComparerParam, result.ValuesParam))
                : new ParameterizedCollectionConstructorInfo(
                    result.Factory!,
                    result.Signature!,
                    GetComparerOptions(result.ComparerParam, result.ValuesParam))))
            .FirstOrDefault();

        static int RankComparer(CollectionConstructorParameter? comparer)
        {
            return comparer switch
            {
                // Rank optional comparers higher than mandatory ones.
                CollectionConstructorParameter.EqualityComparerOptional or
                CollectionConstructorParameter.ComparerOptional => 0,
                CollectionConstructorParameter.EqualityComparer or
                CollectionConstructorParameter.Comparer => 1,
                _ => int.MaxValue,
            };
        }

        int RankStrategy(CollectionConstructorParameter? valueParam)
        {
            return (addMethod, valueParam) switch
            {
                (not null, null) => 0, // Mutable collections with add methods are ranked highest.
                (null, not null) => 0, // Parameterized constructors are ranked highest if no add method is available.
                _ => int.MaxValue, // Every other combination is ranked lowest.
            };
        }

        int RankValuePerformance(CollectionConstructorParameter? valueParam, MethodBase factory, CollectionConstructorParameter[] signature)
        {
            if (!MemberAccessor.IsCollectionConstructorSupported(factory, signature))
            {
                return int.MaxValue; // Unsupported constructors are ranked lowest.
            }

            return valueParam switch
            {
                null => 0, // mutable collection constructors are ranked highest.
                CollectionConstructorParameter.Span => 1, // Constructors accepting span.
                CollectionConstructorParameter.List => 2, // Constructors accepting List.
                _ => 3, // Everything is ranked equally.
            };
        }

        static CollectionComparerOptions GetComparerOptions(CollectionConstructorParameter? options, CollectionConstructorParameter? valuesParam)
        {
            return (options ?? valuesParam) switch
            {
                CollectionConstructorParameter.EqualityComparer or
                CollectionConstructorParameter.EqualityComparerOptional or
                CollectionConstructorParameter.HashSet or
                CollectionConstructorParameter.Dictionary => CollectionComparerOptions.EqualityComparer,
                CollectionConstructorParameter.Comparer or
                CollectionConstructorParameter.ComparerOptional => CollectionComparerOptions.Comparer,
                _ => CollectionComparerOptions.None,
            };
        }
    }

    static private CollectionConstructorParameter[]? ClassifyCollectionConstructor<TElement, TKey>(
        Type collectionType,
        MethodBase method,
        bool allowMutableCtor,
        out CollectionConstructorParameter? valuesParameter,
        out CollectionConstructorParameter? comparerParameter,
        Type? correspondingDictionaryType = null,
        Type? correspondingTupleEnumerable = null)
    {
        Debug.Assert(method is MethodInfo { IsStatic: true } or ConstructorInfo { IsStatic: false });
        NullabilityInfoContext? nullabilityInfoContext = CreateNullabilityInfoContext();
        ParameterInfo[] parameters = method.GetParameters();
        valuesParameter = null;
        comparerParameter = null;

        Type returnType = method is MethodInfo methodInfo
            ? methodInfo.ReturnType
            : ((ConstructorInfo)method).DeclaringType!;

        if (!collectionType.IsAssignableFrom(returnType))
        {
            return null; // The method does not return a matching type.
        }

        if (parameters.Length == 0)
        {
            if (!allowMutableCtor)
            {
                return null;
            }

            return [];
        }

        var signature = new CollectionConstructorParameter[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        {
            CollectionConstructorParameter parameterType = signature[i] = ClassifyParameter(parameters[i]);

            if (parameterType is CollectionConstructorParameter.Unrecognized ||
                Array.IndexOf(signature, parameterType, 0, i) >= 0)
            {
                // Unrecognized or duplicated parameter type.
                return null;
            }

            bool isValuesParameter = parameterType is
                CollectionConstructorParameter.Span or
                CollectionConstructorParameter.List or
                CollectionConstructorParameter.HashSet or
                CollectionConstructorParameter.Dictionary or
                CollectionConstructorParameter.TupleEnumerable;

            if (isValuesParameter)
            {
                if (valuesParameter is not null)
                {
                    return null; // duplicate values parameter.
                }

                valuesParameter = parameterType;
                continue;
            }

            bool isComparerParameter = parameterType is
                CollectionConstructorParameter.Comparer or
                CollectionConstructorParameter.ComparerOptional or
                CollectionConstructorParameter.EqualityComparer or
                CollectionConstructorParameter.EqualityComparerOptional;

            if (isComparerParameter)
            {
                if (comparerParameter is not null)
                {
                    return null; // duplicate comparer parameter.
                }

                comparerParameter = parameterType;
            }
        }

        return signature;

        CollectionConstructorParameter ClassifyParameter(ParameterInfo parameter)
        {
            Type parameterType = parameter.ParameterType;

            if (collectionType.IsAssignableFrom(parameterType))
            {
                // The parameter is the same type as the collection, creating a recursive relationship.
                return CollectionConstructorParameter.Unrecognized;
            }

            if (parameterType == typeof(ReadOnlySpan<TElement>))
            {
                return CollectionConstructorParameter.Span;
            }

            if (parameterType.IsAssignableFrom(typeof(List<TElement>)))
            {
                return CollectionConstructorParameter.List;
            }

            if (parameterType.IsAssignableFrom(typeof(HashSet<TElement>)))
            {
                return CollectionConstructorParameter.HashSet;
            }

            if (parameterType.IsAssignableFrom(correspondingDictionaryType))
            {
                return CollectionConstructorParameter.Dictionary;
            }

            if (parameterType == correspondingTupleEnumerable)
            {
                return CollectionConstructorParameter.TupleEnumerable;
            }

            if (parameterType == typeof(int) && parameter.Name is "capacity" or "initialCapacity")
            {
                return parameter.IsOptional
                    ? CollectionConstructorParameter.CapacityOptional
                    : CollectionConstructorParameter.Capacity;
            }

            if (parameterType == typeof(IEqualityComparer<TKey>))
            {
                return AcceptsNullComparer()
                    ? CollectionConstructorParameter.EqualityComparerOptional
                    : CollectionConstructorParameter.EqualityComparer;
            }

            if (parameterType == typeof(IComparer<TKey>))
            {
                return AcceptsNullComparer()
                    ? CollectionConstructorParameter.ComparerOptional
                    : CollectionConstructorParameter.Comparer;
            }

            return CollectionConstructorParameter.Unrecognized;

            bool AcceptsNullComparer()
            {
                return parameter.IsOptional ||
                    nullabilityInfoContext?.Create(parameter).WriteState is NullabilityState.Nullable ||
                    // Trust that all System.Collections types tolerate a nullable comparer,
                    // with the exception of ConcurrentDictionary which fails with null in framework.
                    (collectionType.Namespace?.StartsWith("System.Collections", StringComparison.Ordinal) is true &&
                     (!ReflectionHelpers.IsNetFramework || collectionType.Name is not "ConcurrentDictionary`2"));
            }
        }
    }

    private static bool TryGetInlineArrayElementType(Type type, [NotNullWhen(true)] out Type? elementType, out int length)
    {
        elementType = null;
        length = 0;

        if (!type.IsValueType)
        {
            return false;
        }

#if NET
        foreach (CustomAttributeData attr in type.GetCustomAttributesData())
        {
            if (attr.AttributeType == typeof(InlineArrayAttribute))
            {
                length = (int)attr.ConstructorArguments[0].Value!;
                FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (fields.Length > 0)
                {
                    elementType = fields[0].FieldType;
                    return true;
                }
            }
        }
#endif

        // Check for single fixed buffer field
        if (type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) is [FieldInfo field])
        {
            foreach (CustomAttributeData attr in field.GetCustomAttributesData())
            {
                if (attr.AttributeType == typeof(FixedBufferAttribute))
                {
                    elementType = (Type)attr.ConstructorArguments[0].Value!;
                    length = (int)attr.ConstructorArguments[1].Value!;
                    return true;
                }
            }
        }

        return false;
    }

    internal static MethodShapeInfo CreateMethodShapeInfo(
        MethodInfo methodInfo,
        MethodShapeAttribute? shapeAttribute,
        NullabilityInfoContext? nullabilityCtx)
    {
        if (methodInfo.IsGenericMethodDefinition)
        {
            throw new NotSupportedException($"Cannot generate shape for generic method '{methodInfo}'.");
        }

        ParameterInfo[] parameters = methodInfo.GetParameters();
        if (parameters.FirstOrDefault(param => param.IsOut || !param.GetEffectiveParameterType().CanBeGenericArgument()) is { } param)
        {
            throw new NotSupportedException($"Method '{methodInfo}' contains unsupported parameter type '{param.Name}'.");
        }

        if (methodInfo.ReturnType != typeof(void) && !methodInfo.ReturnParameter.GetEffectiveParameterType().CanBeGenericArgument())
        {
            throw new NotSupportedException($"Method '{methodInfo}' has an unsupported return type '{methodInfo.ReturnType}'.");
        }

        int i = 0;
        var parameterShapeInfos = new MethodParameterShapeInfo[parameters.Length];
        foreach (ParameterInfo parameter in parameters)
        {
            ParameterShapeAttribute? parameterShapeAttribute = parameter.GetCustomAttribute<ParameterShapeAttribute>();
            string? paramName = parameterShapeAttribute?.Name ?? parameter.Name;
            if (string.IsNullOrEmpty(paramName))
            {
                throw new NotSupportedException($"The method '{methodInfo.DeclaringType}.{methodInfo.Name}' has had its parameter names trimmed.");
            }

            bool? isRequired = parameterShapeAttribute?.IsRequiredSpecified is true ? parameterShapeAttribute.IsRequired : null;
            parameterShapeInfos[i++] = new MethodParameterShapeInfo(
                parameter,
                isNonNullable: parameter.IsNonNullableAnnotation(nullabilityCtx),
                logicalName: paramName,
                isRequired: isRequired);
        }

        string name = shapeAttribute?.Name ?? methodInfo.Name;
        Type returnType = methodInfo.GetEffectiveReturnType() ?? typeof(Unit);
        return new MethodShapeInfo(returnType, methodInfo, parameterShapeInfos, name: name);
    }

    internal static NullabilityInfoContext? CreateNullabilityInfoContext()
    {
        return ReflectionHelpers.IsNullabilityInfoContextSupported ? new() : null;
    }

    internal static readonly EqualityComparer<(Type Type, string Name)> CtorParameterEqualityComparer =
        CommonHelpers.CreateTupleComparer(
            EqualityComparer<Type>.Default,
            CommonHelpers.CamelCaseInvariantComparer.Instance);
}
