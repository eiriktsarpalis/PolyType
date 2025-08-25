using PolyType.Abstractions;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace PolyType.ReflectionProvider;

[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
internal abstract class ReflectionObjectTypeShape<T>(ReflectionTypeShapeProvider provider, ReflectionTypeShapeOptions options) : ReflectionTypeShape<T>(provider, options), IObjectTypeShape<T>
{
    public sealed override TypeShapeKind Kind => TypeShapeKind.Object;
    public sealed override object? Accept(TypeShapeVisitor visitor, object? state = null) => visitor.VisitObject(this, state);

    public virtual bool IsRecordType => false;
    public virtual bool IsTupleType => false;

    public IReadOnlyList<IPropertyShape> Properties => _properties ?? CommonHelpers.ExchangeIfNull(ref _properties, GetProperties().AsReadOnlyList());
    private IReadOnlyList<IPropertyShape>? _properties;

    public IConstructorShape? Constructor
    {
        get
        {
            if (!_isConstructorResolved)
            {
                IConstructorShape? constructor = GetConstructor();
                if (constructor is not null)
                {
                    constructor = CommonHelpers.ExchangeIfNull(ref _constructor, constructor);
                }

                Volatile.Write(ref _isConstructorResolved, true);
            }

            return _constructor;
        }
    }

    private IConstructorShape? _constructor;
    private bool _isConstructorResolved;

    protected abstract IEnumerable<IPropertyShape> GetProperties();
    protected abstract IConstructorShape? GetConstructor();
}

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class DefaultReflectionObjectTypeShape<T>(ReflectionTypeShapeProvider provider, bool disableMemberResolution, ReflectionTypeShapeOptions options)
    : ReflectionObjectTypeShape<T>(provider, options)
{
    public override bool IsRecordType => _isRecord ??= typeof(T).IsRecordType();
    private bool? _isRecord;

    public override bool IsTupleType => _isTuple ??= typeof(T).IsTupleType();
    private bool? _isTuple;

    public bool IsSimpleType => _isSimpleType ??= DetermineIsSimpleType(typeof(T));
    private bool? _isSimpleType;

    protected override IConstructorShape? GetConstructor()
    {
        if (typeof(T).IsAbstract || IsSimpleType)
        {
            return null;
        }

        if (IsTupleType)
        {
            IMethodShapeInfo ctorInfo = ReflectionTypeShapeProvider.CreateTupleConstructorShapeInfo(typeof(T));
            return Provider.CreateConstructor(this, ctorInfo);
        }

        PropertyShapeInfo[] allMembers;
        MemberInitializerShapeInfo[] settableMembers;
        NullabilityInfoContext? nullabilityCtx = ReflectionTypeShapeProvider.CreateNullabilityInfoContext();

        (ConstructorInfo Ctor, ParameterInfo[] Parameters, bool HasShapeAttribute)[] ctorCandidates = [.. GetCandidateConstructors()];
        if (ctorCandidates.Length == 0)
        {
            if (typeof(T).IsValueType)
            {
                // If no explicit ctor has been defined, use the implicit default constructor for structs.
                allMembers = [.. GetMembers(nullabilityCtx)];
                settableMembers = GetSettableMembers(allMembers, ctorSetsRequiredMembers: false);
                bool hasRequiredOrInitOnlyMembers = settableMembers.Any(m => m.IsRequired || m.IsInitOnly);
                MethodShapeInfo defaultCtorInfo = CreateDefaultConstructor(hasRequiredOrInitOnlyMembers ? settableMembers : []);
                return Provider.CreateConstructor(this, defaultCtorInfo);
            }

            return null;
        }

        ConstructorInfo? constructorInfo;
        ParameterInfo[] parameters;
        allMembers = [.. GetMembers(nullabilityCtx)];

        if (ctorCandidates.Length == 1)
        {
            (constructorInfo, parameters, _) = ctorCandidates[0];
        }
        else
        {
            // If the type defines more than one constructors, pick one using the following rules:
            // 1. Prefer [ConstructorShape] annotated constructors.
            // 2. Maximize the number of parameters that match read-only properties/fields.
            // 3. Minimize the number of parameters not corresponding to any property/field.

            Dictionary<(Type, string), bool> readableMembers = allMembers
                .Where(member => member.MemberInfo is PropertyInfo { CanRead: true } or FieldInfo)
                .ToDictionary(
                    keySelector: member => (member.MemberInfo.GetMemberType(), member.MemberInfo.Name),
                    elementSelector: member => member.MemberInfo switch
                    {
                        PropertyInfo prop => !prop.CanWrite,
                        var field => ((FieldInfo)field).IsInitOnly,
                    },
                    comparer: ReflectionTypeShapeProvider.CtorParameterEqualityComparer);

            (constructorInfo, parameters, _) = ctorCandidates
                .OrderByDescending(ctor =>
                {
                    int matchingReadOnlyMemberParamCount = 0;
                    int unmatchedParamCount = 0;
                    foreach (ParameterInfo param in ctor.Parameters)
                    {
                        if (readableMembers.TryGetValue((param.ParameterType, param.Name!), out bool isReadOnly))
                        {
                            // Do not count settable members as they can set after any constructor.
                            if (isReadOnly)
                            {
                                matchingReadOnlyMemberParamCount++;
                            }
                        }
                        else
                        {
                            unmatchedParamCount++;
                        }
                    }

                    return (ctor.HasShapeAttribute, matchingReadOnlyMemberParamCount, -unmatchedParamCount);
                })
                .FirstOrDefault();
        }

        var parameterShapeInfos = new MethodParameterShapeInfo[parameters.Length];
        int i = 0;

        foreach (ParameterInfo parameter in parameters)
        {
            Debug.Assert(!parameter.IsOut, "must have been filtered earlier");

            if (string.IsNullOrEmpty(parameter.Name))
            {
                throw new NotSupportedException($"The constructor for type '{parameter.Member.DeclaringType}' has had its parameter names trimmed.");
            }

            Type parameterType = parameter.GetEffectiveParameterType();
            bool isNonNullable = parameter.IsNonNullableAnnotation(nullabilityCtx);
            PropertyShapeInfo? matchingMember = allMembers.FirstOrDefault(member =>
                member.MemberInfo.GetMemberType() == parameterType &&
                CommonHelpers.CamelCaseInvariantComparer.Instance.Equals(parameter.Name, member.MemberInfo.Name));

            ParameterShapeAttribute? parameterShapeAttribute = parameter.GetCustomAttribute<ParameterShapeAttribute>();
            string? logicalName = parameterShapeAttribute?.Name;
            if (logicalName is null && matchingMember is not null)
            {
                // If no custom name is specified, adopt the name from the matching property.
                logicalName = matchingMember.LogicalName ?? matchingMember.MemberInfo.Name;
            }

            bool? isRequired = parameterShapeAttribute?.IsRequiredSpecified is true ? parameterShapeAttribute.IsRequired : null;

            parameterShapeInfos[i++] = new(parameter, isNonNullable, matchingMember?.MemberInfo, logicalName, isRequired);
        }

        bool setsRequiredMembers = constructorInfo.SetsRequiredMembers();
        settableMembers = GetSettableMembers(allMembers, setsRequiredMembers);
        List<MemberInitializerShapeInfo>? memberInitializers = null;

        if (parameters.Length > 0 || settableMembers.Any(m => m.IsRequired || m.IsInitOnly))
        {
            // Constructors with parameters, or constructors with required or init-only members
            // are deemed to be parameterized, in which case we also include *all* settable
            // members in the shape signature.
            foreach (MemberInitializerShapeInfo memberInitializer in settableMembers)
            {
                if (!memberInitializer.IsRequired && parameterShapeInfos.Any(p => p.MatchingMember == memberInitializer.MemberInfo))
                {
                    // Deduplicate any properties whose signature matches a constructor parameter.
                    continue;
                }

                (memberInitializers ??= []).Add(memberInitializer);
            }
        }

        var ctorShapeInfo = new MethodShapeInfo(typeof(T), constructorInfo, parameterShapeInfos, memberInitializers?.ToArray());
        return Provider.CreateConstructor(this, ctorShapeInfo);

        static MethodShapeInfo CreateDefaultConstructor(MemberInitializerShapeInfo[]? memberInitializers)
            => new(typeof(T), method: null, parameters: [], memberInitializers: memberInitializers);

        static MemberInitializerShapeInfo[] GetSettableMembers(PropertyShapeInfo[] allMembers, bool ctorSetsRequiredMembers)
        {
            return allMembers
                .Where(m => m.MemberInfo is PropertyInfo { CanWrite: true } or FieldInfo { IsInitOnly: false })
                .Select(m => new MemberInitializerShapeInfo(m.MemberInfo, m.LogicalName, ctorSetsRequiredMembers, m.IsSetterNonNullable, m.IsRequiredByAttribute))
                .OrderByDescending(m => m.IsRequired || m.IsInitOnly) // Shift required or init members first
                .ToArray();
        }

        IEnumerable<(ConstructorInfo, ParameterInfo[], bool HasShapeAttribute)> GetCandidateConstructors()
        {
            bool foundCtorWithShapeAttribute = false;
            foreach (ConstructorInfo constructorInfo in typeof(T).GetConstructors(AllInstanceMembers))
            {
                bool hasShapeAttribute = constructorInfo.GetCustomAttribute<ConstructorShapeAttribute>() != null;
                if (hasShapeAttribute)
                {
                    if (foundCtorWithShapeAttribute)
                    {
                        throw new InvalidOperationException(
                            $"The type '{typeof(T)}' has duplicate {nameof(ConstructorShapeAttribute)} annotations.");
                    }

                    foundCtorWithShapeAttribute = true;
                }
                else if (!constructorInfo.IsPublic)
                {
                    // Skip unannotated constructors that aren't public.
                    continue;
                }

                ParameterInfo[] parameters = constructorInfo.GetParameters();
                if (parameters.Any(param => param.IsOut || !param.GetEffectiveParameterType().CanBeGenericArgument()))
                {
                    // Skip constructors with unsupported parameter types or out parameters
                    continue;
                }

                if (IsRecordType && parameters is [ParameterInfo singleParam] &&
                    singleParam.ParameterType == typeof(T))
                {
                    // Skip the copy constructor in record types
                    continue;
                }

                yield return (constructorInfo, parameters, hasShapeAttribute);
            }
        }
    }

    protected override IEnumerable<IPropertyShape> GetProperties()
    {
        if (IsSimpleType)
        {
            yield break;
        }

        if (IsTupleType)
        {
            int i = 0;
            foreach (var field in ReflectionHelpers.EnumerateTupleMemberPaths(typeof(T)))
            {
                PropertyShapeInfo propertyShapeInfo = new(typeof(T), field.Member, field.Member, field.ParentMembers, field.LogicalName);
                yield return Provider.CreateProperty(this, propertyShapeInfo, position: i++);
            }

            yield break;
        }

        int p = 0;
        NullabilityInfoContext? nullabilityCtx = ReflectionTypeShapeProvider.CreateNullabilityInfoContext();
        foreach (PropertyShapeInfo member in GetMembers(nullabilityCtx))
        {
            yield return Provider.CreateProperty(this, member, position: p++);
        }
    }

    private IEnumerable<PropertyShapeInfo> GetMembers(NullabilityInfoContext? nullabilityCtx)
    {
        Debug.Assert(!IsSimpleType);
        List<PropertyShapeInfo> results = [];
        HashSet<string> membersInScope = new(StringComparer.Ordinal);
        bool isOrderSpecified = false;

        foreach (Type current in typeof(T).GetSortedTypeHierarchy())
        {
            foreach (PropertyInfo propertyInfo in current.GetProperties(AllInstanceMembers))
            {
                if (propertyInfo.GetIndexParameters().Length == 0 &&
                    propertyInfo.PropertyType.CanBeGenericArgument() &&
                    !propertyInfo.IsExplicitInterfaceImplementation() &&
                    !IsOverriddenOrShadowed(propertyInfo))
                {
                    HandleMember(propertyInfo, nullabilityCtx);
                }
            }

            foreach (FieldInfo fieldInfo in current.GetFields(AllInstanceMembers))
            {
                if (fieldInfo.FieldType.CanBeGenericArgument() &&
                    !IsOverriddenOrShadowed(fieldInfo))
                {
                    HandleMember(fieldInfo, nullabilityCtx);
                }
            }
        }

        return isOrderSpecified ? results.OrderBy(r => r.Order) : results;

        bool IsOverriddenOrShadowed(MemberInfo memberInfo) => !membersInScope.Add(memberInfo.Name);

        void HandleMember(MemberInfo memberInfo, NullabilityInfoContext? nullabilityCtx)
        {
            // Use the most derived member for attribute resolution but
            // use the base definition to determine the member signatures
            // (overrides might declare partial signatures, e.g. only overriding the getter or setter).
            MemberInfo attributeProvider = memberInfo;
            memberInfo = memberInfo is PropertyInfo p ? p.GetBaseDefinition() : memberInfo;

            PropertyShapeAttribute? propertyAttr = attributeProvider.GetCustomAttribute<PropertyShapeAttribute>(inherit: true);
            string? logicalName = null;
            bool includeNonPublic = false;
            int order = 0;
            bool? isRequiredByAttribute = null;

            if (propertyAttr != null)
            {
                // If the attribute is present, use the value of the Ignore property to determine its inclusion.
                if (propertyAttr.Ignore)
                {
                    return;
                }

                logicalName = propertyAttr.Name;
                if (propertyAttr.Order != 0)
                {
                    order = propertyAttr.Order;
                    isOrderSpecified = true;
                }

                includeNonPublic = true;
                if (propertyAttr.IsRequiredSpecified)
                {
                    isRequiredByAttribute = propertyAttr.IsRequired;
                }
            }
            else
            {
                // If no attribute is present, only include members that have at least one public accessor.
                memberInfo.ResolveAccessibility(out bool isGetterPublic, out bool isSetterPublic);
                if (!isGetterPublic && !isSetterPublic)
                {
                    return;
                }
            }

            memberInfo.ResolveNullableAnnotation(nullabilityCtx, out bool isGetterNonNullable, out bool isSetterNonNullable);
            results.Add(new(
                typeof(T),
                memberInfo,
                attributeProvider,
                LogicalName: logicalName,
                Order: order,
                IncludeNonPublicAccessors: includeNonPublic,
                IsGetterNonNullable: isGetterNonNullable,
                IsSetterNonNullable: isSetterNonNullable,
                IsRequiredByAttribute: isRequiredByAttribute));
        }
    }

    private const BindingFlags AllInstanceMembers = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    private bool DetermineIsSimpleType(Type type)
    {
        // A primitive or self-contained value type that
        // shouldn't expose its properties or constructors.
        return disableMemberResolution ||
            type.IsPrimitive ||
            type == typeof(object) ||
            type == typeof(string) ||
            type == typeof(decimal) ||
            type == typeof(DateTime) ||
            type == typeof(DateTimeOffset) ||
            type == typeof(TimeSpan) ||
            type == typeof(Guid) ||
            type == typeof(Version) ||
            type == typeof(Uri) ||
            type == typeof(System.Numerics.BigInteger) ||
#if NET
            type == typeof(UInt128) ||
            type == typeof(Int128) ||
            type == typeof(Half) ||
            type == typeof(DateOnly) ||
            type == typeof(TimeOnly) ||
            type == typeof(System.Text.Rune) ||
#endif
            type == typeof(System.Threading.CancellationToken) ||
            typeof(MemberInfo).IsAssignableFrom(type) ||
            typeof(Delegate).IsAssignableFrom(type) ||
            typeof(Exception).IsAssignableFrom(type) ||
            typeof(Task).IsAssignableFrom(type) ||
            type == typeof(System.Threading.Tasks.ValueTask) ||
            (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(System.Threading.Tasks.ValueTask<>));
    }
}