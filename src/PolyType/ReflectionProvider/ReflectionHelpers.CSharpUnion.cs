using PolyType.Utilities;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace PolyType.ReflectionProvider;

/// <summary>
/// Provides reflection helpers for C# union discovery.
/// </summary>
internal static partial class ReflectionHelpers
{
    private const BindingFlags DeclaredMembers = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    [RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
    [RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
    public static bool TryResolveCSharpUnionMetadata(Type unionType, [NotNullWhen(true)] out CSharpUnionInfo? unionInfo)
    {
        unionInfo = null;
        if (!unionType.GetCustomAttributesData().Any(attribute => attribute.AttributeType.FullName is "System.Runtime.CompilerServices.UnionAttribute"))
        {
            return false;
        }

        if (!unionType.IsClass && !unionType.IsValueType)
        {
            throw new NotSupportedException($"C# union type '{unionType}' must be a class or struct.");
        }

        Type declarationType = unionType.IsGenericType ? unionType.GetGenericTypeDefinition() : unionType;
        Type? memberProvider = declarationType.GetNestedTypes(BindingFlags.Public)
            .FirstOrDefault(type => type is { IsInterface: true, Name: "IUnionMembers" } &&
                type.GetGenericArguments().Length == declarationType.GetGenericArguments().Length);

        Type definingType = memberProvider ?? declarationType;
        Dictionary<Type, Type> typeArguments = definingType.GetGenericArguments()
            .Zip(unionType.GetGenericArguments(), (parameter, argument) => (parameter, argument))
            .ToDictionary(pair => pair.parameter, pair => pair.argument);

        if (memberProvider is not null && !CloseType(memberProvider).IsAssignableFrom(unionType))
        {
            throw new NotSupportedException($"C# union type '{unionType}' must implement its nested IUnionMembers interface.");
        }

        if (memberProvider is null && unionType.IsAbstract)
        {
            throw new NotSupportedException($"Abstract C# union type '{unionType}' must provide union factory methods.");
        }

        Type[] definingHierarchy = GetMemberLookupHierarchy(definingType);

        PropertyInfo[] valueProperties = definingHierarchy
            .SelectMany(type => type.GetProperties(DeclaredMembers))
            .Where(property => property is { Name: "Value", PropertyType: { } propertyType, GetMethod: { IsPublic: true, IsStatic: false } } &&
                propertyType == typeof(object) && property.GetIndexParameters().Length == 0)
            .ToArray();

        PropertyInfo[] visibleValueProperties = valueProperties
            .Where(property => !valueProperties.Any(other => HidesMember(other, property)))
            .ToArray();

        if (visibleValueProperties is not [PropertyInfo valueProperty])
        {
            throw new NotSupportedException($"C# union type '{unionType}' must have an unambiguous public instance Value getter of type object.");
        }

        MethodBase[] creationMembers = memberProvider is null
            ? declarationType.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .Where(IsCreationMember).Cast<MethodBase>().ToArray()
            : GetUnionFactories();

        if (creationMembers.Length == 0)
        {
            throw new NotSupportedException($"C# union type '{unionType}' must have at least one public creation member with a single by-value or in parameter.");
        }

        HashSet<Type> caseTypes = [];
        HashSet<string> caseNames = new(StringComparer.Ordinal);
        List<CSharpUnionCaseInfo> cases = [];
        NullabilityInfoContext? nullabilityContext = ReflectionTypeShapeProvider.CreateNullabilityInfoContext();
        Dictionary<Type, MethodInfo> tryGetValueMethods = new();
        foreach (MethodInfo method in unionType.GetMember("TryGetValue", MemberTypes.Method, BindingFlags.Public | BindingFlags.Instance))
        {
            if (method.ReturnType == typeof(bool) && !method.IsGenericMethod &&
                method.GetParameters() is [{ IsOut: true, ParameterType: { IsByRef: true } parameterType }] &&
                !tryGetValueMethods.ContainsKey(parameterType.GetElementType()!))
            {
                tryGetValueMethods.Add(parameterType.GetElementType()!, method);
            }
        }

        foreach (MethodBase declarationMember in creationMembers)
        {
            MethodBase creationMember = CloseMember(declarationMember);
            ParameterInfo parameter = creationMember.GetParameters()[0];
            Type caseType = parameter.GetParameterType();
            if (!caseType.CanBeGenericArgument())
            {
                throw new NotSupportedException($"C# union type '{unionType}' has unsupported case type '{caseType}'.");
            }

            if (!caseTypes.Add(caseType))
            {
                throw new InvalidOperationException($"C# union type '{unionType}' has multiple creation members for the closed case type '{caseType}'.");
            }

            string name = ReflectionUtilities.GetDerivedTypeShapeName(caseType);
            if (!caseNames.Add(name))
            {
                throw new InvalidOperationException($"C# union type '{unionType}' uses duplicate assignments for the name '{name}'.");
            }

            bool isNullable = caseType.IsNullableStruct() ||
                (!caseType.IsValueType && !parameter.IsNonNullableAnnotation(nullabilityContext));

            if (creationMember is MethodInfo factory)
            {
                creationMember = ResolveInterfaceMethod(unionType, factory);
            }

            tryGetValueMethods.TryGetValue(caseType, out MethodInfo? tryGetValue);
            cases.Add(new(caseType, name, cases.Count, isNullable, creationMember, tryGetValue));
        }

        MethodInfo valueGetter = (MethodInfo)CloseMember(valueProperty.GetMethod!);
        if (memberProvider is not null)
        {
            valueGetter = ResolveInterfaceMethod(unionType, valueGetter);
        }

        unionInfo = new(valueGetter, cases.ToArray());
        return true;

        MethodBase[] GetUnionFactories()
        {
            // Resolve hiding before substitution so that distinct generic cases are not
            // silently removed when a closed instantiation makes their signatures equal.
            MethodInfo[] candidates = definingHierarchy
                .SelectMany(type => type.GetMethods(DeclaredMembers))
                .Where(method => method is { Name: "Create", IsPublic: true, IsStatic: true, IsGenericMethod: false } &&
                    CloseType(method.ReturnType) == unionType && IsCreationMember(method))
                .ToArray();

            return candidates
                .Where(method => !candidates.Any(other => HidesMember(other, method) &&
                    other.GetParameters()[0].ParameterType == method.GetParameters()[0].ParameterType))
                .Cast<MethodBase>()
                .ToArray();
        }

        Type CloseType(Type type)
        {
            if (!type.ContainsGenericParameters)
            {
                return type;
            }

            if (type.IsGenericParameter)
            {
                return typeArguments[type];
            }

            if (type.IsArray)
            {
                Type elementType = CloseType(type.GetElementType()!);
                return type == type.GetElementType()!.MakeArrayType() ? elementType.MakeArrayType() : elementType.MakeArrayType(type.GetArrayRank());
            }

            if (type.IsByRef)
            {
                return CloseType(type.GetElementType()!).MakeByRefType();
            }

            if (type.IsPointer)
            {
                return CloseType(type.GetElementType()!).MakePointerType();
            }

            return type.GetGenericTypeDefinition().MakeGenericType(type.GetGenericArguments().Select(CloseType).ToArray());
        }

        MethodBase CloseMember(MethodBase member)
        {
            Type declaringType = CloseType(member.DeclaringType!);
            return declaringType == member.DeclaringType
                ? member
                : (MethodBase)declaringType.GetMember(member.Name, DeclaredMembers).Single(candidate => candidate.MetadataToken == member.MetadataToken);
        }
    }

    [RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
    [RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
    private static bool IsCreationMember(MethodBase member)
    {
        // RequiresLocationAttribute distinguishes excluded ref readonly parameters from supported in parameters.
        return member.GetParameters() is [ParameterInfo parameter] &&
            !parameter.IsOut && (!parameter.ParameterType.IsByRef ||
                (parameter.IsIn && parameter.GetCustomAttributesData().Any(attribute =>
                    attribute.AttributeType.FullName is "System.Runtime.CompilerServices.IsReadOnlyAttribute") &&
                    !parameter.GetCustomAttributesData().Any(attribute =>
                        attribute.AttributeType.FullName is "System.Runtime.CompilerServices.RequiresLocationAttribute")));
    }

    [RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
    [RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
    private static Type[] GetMemberLookupHierarchy(Type type)
    {
        if (!type.IsInterface)
        {
            return [.. type.GetSortedTypeHierarchy()];
        }

        List<Type> remaining = [type, .. type.GetInterfaces()];
        Type[] result = new Type[remaining.Count];
        for (int i = 0; i < result.Length; i++)
        {
            Type next = remaining.First(candidate => !remaining.Any(other => other.GetInterfaces().Contains(candidate)));
            result[i] = next;
            remaining.Remove(next);
        }

        return result;
    }

    [RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
    [RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
    private static bool HidesMember(MemberInfo member, MemberInfo other)
    {
        return member.DeclaringType != other.DeclaringType &&
            (member.DeclaringType!.IsInterface
                ? member.DeclaringType.GetInterfaces().Contains(other.DeclaringType!)
                : other.DeclaringType!.IsAssignableFrom(member.DeclaringType));
    }

    [RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
    [RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
    private static MethodInfo ResolveInterfaceMethod(Type unionType, MethodInfo method)
    {
        if (!method.IsVirtual)
        {
            return method;
        }

        InterfaceMapping mapping = unionType.GetInterfaceMap(method.DeclaringType!);
        int index = Array.IndexOf(mapping.InterfaceMethods, method);
        if (index >= 0 && mapping.TargetMethods[index] is MethodInfo target && !(target.IsStatic && target.IsAbstract))
        {
            return target;
        }

        throw new NotSupportedException($"C# union type '{unionType}' has no implementation for union member '{method}'.");
    }
}

internal sealed record CSharpUnionInfo(MethodInfo ValueGetter, CSharpUnionCaseInfo[] Cases);
internal sealed record CSharpUnionCaseInfo(Type Type, string Name, int Index, bool IsNullable, MethodBase CreationMember, MethodInfo? TryGetValue);
