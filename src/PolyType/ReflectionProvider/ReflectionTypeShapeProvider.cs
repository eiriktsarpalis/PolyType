using PolyType.Abstractions;
using PolyType.ReflectionProvider.MemberAccessors;
using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

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

    private readonly ConcurrentDictionary<Type, ITypeShape> _cache = new();
    private readonly Func<Type, ITypeShape> _typeShapeFactory;

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
    public ITypeShape<T> GetShape<T>() => (ITypeShape<T>)GetShape(typeof(T));

    /// <summary>
    /// Gets a <see cref="ITypeShape"/> instance corresponding to the supplied type.
    /// </summary>
    /// <param name="type">The type for which a shape is requested.</param>
    /// <returns>
    /// A <see cref="ITypeShape"/> instance corresponding to the current type.
    /// </returns>
    /// <exception cref="ArgumentNullException">The <paramref name="type"/> argument is null.</exception>
    /// <exception cref="ArgumentException">The <paramref name="type"/> cannot be a generic argument.</exception>
    public ITypeShape GetShape(Type type)
    {
        Throw.IfNull(type);
        return _cache.GetOrAdd(type, _typeShapeFactory);
    }

    internal ITypeShape CreateTypeShapeCore(Type type, bool allowUnionShapes = true)
    {
        DebugExt.Assert(type != null);

        if (!type.CanBeGenericArgument())
        {
            throw new ArgumentException("Type cannot be a generic parameter", nameof(type));
        }

        return DetermineTypeKind(type, allowUnionShapes, out TypeShapeAttribute? typeShapeAttribute, out FSharpUnionInfo? fSharpUnionInfo) switch
        {
            TypeShapeKind.Enumerable => CreateEnumerableShape(type),
            TypeShapeKind.Dictionary => CreateDictionaryShape(type),
            TypeShapeKind.Enum => CreateEnumShape(type),
            TypeShapeKind.Optional => CreateOptionalShape(type, fSharpUnionInfo),
            TypeShapeKind.Object => CreateObjectShape(type, disableMemberResolution: false),
            TypeShapeKind.Surrogate => CreateSurrogateShape(type, typeShapeAttribute),
            TypeShapeKind.Union => CreateUnionTypeShape(type, fSharpUnionInfo),
            TypeShapeKind.None or _ => CreateObjectShape(type, disableMemberResolution: true),
        };
    }

    private ITypeShape CreateObjectShape(Type type, bool disableMemberResolution)
    {
        Type objectShapeTy = typeof(DefaultReflectionObjectTypeShape<>).MakeGenericType(type);
        return (ITypeShape)Activator.CreateInstance(objectShapeTy, this, disableMemberResolution)!;
    }

    private ITypeShape CreateSurrogateShape(Type type, TypeShapeAttribute? typeShapeAttribute)
    {
        Type? marshallerType = typeShapeAttribute?.Marshaller;
        DebugExt.Assert(marshallerType != null);

        if (marshallerType.IsGenericTypeDefinition)
        {
            // Generic marshallers are applied the type parameters from the declaring type.
            marshallerType = marshallerType.MakeGenericType(type.GetGenericArguments());
        }

        // First check that the marshaller implements exactly one IMarshaller<,> for the source type.
        Type? matchingSurrogate = null;
        foreach (Type interfaceType in marshallerType.GetAllInterfaces())
        {
            if (interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(IMarshaller<,>))
            {
                Type[] genericArgs = interfaceType.GetGenericArguments();
                if (genericArgs[0] == type)
                {
                    if (matchingSurrogate != null)
                    {
                        throw new InvalidOperationException($"The type '{marshallerType}' defines conflicting surrogate marshallers from type '{type}'.");
                    }

                    matchingSurrogate = genericArgs[1];
                }
            }
        }

        if (matchingSurrogate is null)
        {
            throw new InvalidOperationException($"The type '{marshallerType}' does not define any surrogate marshallers from type '{type}'.");
        }

        object bijection = Activator.CreateInstance(marshallerType)!;
        Type surrogateTypeShapeTy = typeof(ReflectionSurrogateTypeShape<,>).MakeGenericType(type, matchingSurrogate);
        return (ITypeShape)Activator.CreateInstance(surrogateTypeShapeTy, bijection, this)!;
    }

    private IEnumerableTypeShape CreateEnumerableShape(Type type)
    {
        Debug.Assert(typeof(IEnumerable).IsAssignableFrom(type) || type.IsMemoryType(out _, out _));

        if (type.IsArray)
        {
            Type elementType = type.GetElementType()!;
            int rank = type.GetArrayRank();

            if (rank == 1)
            {
                Type enumerableTypeTy = typeof(ReflectionArrayTypeShape<>).MakeGenericType(elementType);
                return (IEnumerableTypeShape)Activator.CreateInstance(enumerableTypeTy, this)!;
            }
            else
            {
                Type enumerableTypeTy = typeof(MultiDimensionalArrayTypeShape<,>).MakeGenericType(type, elementType);
                return (IEnumerableTypeShape)Activator.CreateInstance(enumerableTypeTy, this, rank)!;
            }
        }

        foreach (Type interfaceTy in type.GetAllInterfaces())
        {
            if (interfaceTy.IsGenericType)
            {
                Type genericInterfaceTypeDef = interfaceTy.GetGenericTypeDefinition();

                if (genericInterfaceTypeDef == typeof(IEnumerable<>))
                {
                    Type elementType = interfaceTy.GetGenericArguments()[0];
                    Type enumerableTypeTy = typeof(ReflectionEnumerableTypeOfTShape<,>).MakeGenericType(type, elementType);
                    return (IEnumerableTypeShape)Activator.CreateInstance(enumerableTypeTy, this)!;
                }
            }
        }

        if (type.IsMemoryType(out Type? memoryElementType, out bool isReadOnlyMemory))
        {
            Type shapeType = isReadOnlyMemory ? typeof(ReadOnlyMemoryTypeShape<>) : typeof(MemoryTypeShape<>);
            Type enumerableTypeTy = shapeType.MakeGenericType(memoryElementType);
            return (IEnumerableTypeShape)Activator.CreateInstance(enumerableTypeTy, this)!;
        }

        Type enumerableType = typeof(ReflectionNonGenericEnumerableTypeShape<>).MakeGenericType(type);
        return (IEnumerableTypeShape)Activator.CreateInstance(enumerableType, this)!;
    }

    private IDictionaryTypeShape CreateDictionaryShape(Type type)
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

        return (IDictionaryTypeShape)Activator.CreateInstance(dictionaryTypeTy, this)!;
    }

    private IEnumTypeShape CreateEnumShape(Type enumType)
    {
        Debug.Assert(enumType.IsEnum);
        Type enumTypeTy = typeof(ReflectionEnumTypeShape<,>).MakeGenericType(enumType, Enum.GetUnderlyingType(enumType));
        return (IEnumTypeShape)Activator.CreateInstance(enumTypeTy, this)!;
    }

    private IOptionalTypeShape CreateOptionalShape(Type optionalType, FSharpUnionInfo? fsharpUnionInfo )
    {
        if (fsharpUnionInfo is not null)
        {
            Debug.Assert(fsharpUnionInfo.IsOptional && optionalType.IsGenericType);
            Type fsharpOptionalType = typeof(FSharpOptionTypeShape<,>).MakeGenericType(optionalType, optionalType.GetGenericArguments()[0]);
            return (IOptionalTypeShape)Activator.CreateInstance(fsharpOptionalType, fsharpUnionInfo, this)!;
        }

        Debug.Assert(optionalType.IsNullableStruct());
        Type nullableTypeTy = typeof(ReflectionNullableTypeShape<>).MakeGenericType(optionalType.GetGenericArguments());
        return (IOptionalTypeShape)Activator.CreateInstance(nullableTypeTy, this)!;
    }

    private IUnionTypeShape CreateUnionTypeShape(Type unionType, FSharpUnionInfo? fSharpUnionInfo)
    {
        if (fSharpUnionInfo is not null)
        {
            Type fsharpUnionTypeTy = typeof(FSharpUnionTypeShape<>).MakeGenericType(unionType);
            return (IUnionTypeShape)Activator.CreateInstance(fsharpUnionTypeTy, fSharpUnionInfo, this)!;
        }

        DerivedTypeShapeAttribute[] derivedTypeAttributes = unionType.GetCustomAttributes<DerivedTypeShapeAttribute>().ToArray();
        HashSet<Type> types = new();
        HashSet<int> tags = new();
        HashSet<string> names = new(StringComparer.Ordinal);
        int i = 0;
        foreach (DerivedTypeShapeAttribute derivedTypeAttribute in derivedTypeAttributes)
        {
            if (!unionType.IsAssignableFrom(derivedTypeAttribute.Type))
            {
                throw new InvalidOperationException($"The declared derived type '{derivedTypeAttribute.Type}' is not a valid subtype of '{unionType}'.");
            }

            int tag = derivedTypeAttribute.Tag < 0 ? i : derivedTypeAttribute.Tag;
            string name = derivedTypeAttribute.Name;

            if (!types.Add(derivedTypeAttribute.Type))
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

            i++;
        }

        Type unionTypeTy = typeof(ReflectionUnionTypeShape<>).MakeGenericType(unionType);
        return (IUnionTypeShape)Activator.CreateInstance(unionTypeTy, derivedTypeAttributes, this)!;
    }

    private static TypeShapeKind DetermineTypeKind(Type type, bool allowUnionShapes, out TypeShapeAttribute? typeShapeAttribute, out FSharpUnionInfo? fsharpUnionInfo)
    {
        typeShapeAttribute = type.GetCustomAttribute<TypeShapeAttribute>();
        TypeShapeKind builtInKind = DetermineBuiltInTypeKind(type, allowUnionShapes, typeShapeAttribute, out fsharpUnionInfo);

        if (typeShapeAttribute?.GetRequestedKind() is TypeShapeKind requestedKind && requestedKind != builtInKind)
        {
            Debug.Assert(
                builtInKind is TypeShapeKind.Dictionary or TypeShapeKind.Enumerable or TypeShapeKind.Object or TypeShapeKind.Union,
                "Custom kinds can only be specified on types that are objects, interfaces, or structs.");

            bool isCustomKindSupported = requestedKind switch
            {
                TypeShapeKind.Enum or TypeShapeKind.Optional or TypeShapeKind.Surrogate or TypeShapeKind.Union => false,
                TypeShapeKind.Dictionary => builtInKind is TypeShapeKind.Dictionary,
                TypeShapeKind.Enumerable => builtInKind is TypeShapeKind.Dictionary or TypeShapeKind.Enumerable,
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

    private static TypeShapeKind DetermineBuiltInTypeKind(Type type, bool allowUnionShapes, TypeShapeAttribute? typeShapeAttribute, out FSharpUnionInfo? fsharpUnionInfo)
    {
        fsharpUnionInfo = null;

        if (typeShapeAttribute?.Marshaller is not null)
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

        if (FSharpReflectionHelpers.TryResolveFSharpUnionMetadata(type, out fsharpUnionInfo))
        {
            return fsharpUnionInfo.IsOptional ? TypeShapeKind.Optional : TypeShapeKind.Union;
        }

        if (allowUnionShapes && type.GetCustomAttributesData().Any(attrData => attrData.AttributeType == typeof(DerivedTypeShapeAttribute)))
        {
            return TypeShapeKind.Union;
        }

        if (typeof(IDictionary).IsAssignableFrom(type))
        {
            return TypeShapeKind.Dictionary;
        }

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
            }
        }

        if (typeof(IEnumerable).IsAssignableFrom(type) && type != typeof(string))
        {
            return TypeShapeKind.Enumerable;
        }

        if (type.IsMemoryType(out _, out _))
        {
            // Memory<T> or ReadOnlyMemory<T>
            return TypeShapeKind.Enumerable;
        }

        return TypeShapeKind.Object;
    }

    internal IPropertyShape CreateProperty(IObjectTypeShape declaringType, PropertyShapeInfo propertyShapeInfo)
    {
        Type memberType = propertyShapeInfo.MemberInfo.GetMemberType();
        Type reflectionPropertyType = typeof(ReflectionPropertyShape<,>).MakeGenericType(propertyShapeInfo.DeclaringType, memberType);
        return (IPropertyShape)Activator.CreateInstance(reflectionPropertyType, this, declaringType, propertyShapeInfo)!;
    }

    internal IConstructorShape CreateConstructor(IObjectTypeShape declaringType, IConstructorShapeInfo ctorInfo)
    {
        Type argumentStateType = MemberAccessor.CreateConstructorArgumentStateType(ctorInfo);
        Type reflectionConstructorType = typeof(ReflectionConstructorShape<,>).MakeGenericType(ctorInfo.ConstructedType, argumentStateType);
        return (IConstructorShape)Activator.CreateInstance(reflectionConstructorType, this, declaringType, ctorInfo)!;
    }

    internal IConstructorParameterShape CreateConstructorParameter(Type constructorArgumentState, IConstructorShapeInfo ctorInfo, int position)
    {
        IParameterShapeInfo parameterInfo = ctorInfo.Parameters[position];
        Type reflectionConstructorParameterType = typeof(ReflectionConstructorParameterShape<,>).MakeGenericType(constructorArgumentState, parameterInfo.Type);
        return (IConstructorParameterShape)Activator.CreateInstance(reflectionConstructorParameterType, this, ctorInfo, parameterInfo, position)!;
    }

    internal IUnionCaseShape CreateUnionCaseShape(IUnionTypeShape unionTypeShape, DerivedTypeShapeAttribute derivedTypeAttribute, int index)
    {
        string name = derivedTypeAttribute.Name;
        int tag = derivedTypeAttribute.Tag < 0 ? index : derivedTypeAttribute.Tag;
        Type unionCaseType = typeof(ReflectionUnionCaseShape<,>).MakeGenericType(derivedTypeAttribute.Type, unionTypeShape.Type);
        return (IUnionCaseShape)Activator.CreateInstance(unionCaseType, unionTypeShape, name, tag, index, this)!;
    }

    internal static IConstructorShapeInfo CreateTupleConstructorShapeInfo(Type tupleType)
    {
        Debug.Assert(tupleType.IsTupleType() && tupleType != typeof(ValueTuple));

        if (!tupleType.IsNestedTupleRepresentation())
        {
            // Treat non-nested tuples as regular types.
            ConstructorInfo ctorInfo = tupleType.GetConstructors()[0];
            MethodParameterShapeInfo[] parameters = ctorInfo.GetParameters().Select((p, i) => new MethodParameterShapeInfo(p, isNonNullable: false, logicalName: $"Item{i + 1}")).ToArray();
            return new MethodConstructorShapeInfo(tupleType, ctorInfo, parameters);
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

    internal static NullabilityInfoContext? CreateNullabilityInfoContext()
    {
        return ReflectionHelpers.IsNullabilityInfoContextSupported ? new() : null;
    }

    internal static readonly EqualityComparer<(Type Type, string Name)> CtorParameterEqualityComparer =
        CommonHelpers.CreateTupleComparer(
            EqualityComparer<Type>.Default,
            CommonHelpers.CamelCaseInvariantComparer.Instance);
}
