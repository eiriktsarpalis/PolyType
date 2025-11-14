using PolyType.Abstractions;
using System.Diagnostics;
using System.Reflection;

namespace PolyType.ReflectionProvider;

internal interface IParameterShapeInfo
{
    Type Type { get; }
    string Name { get; }
    ParameterKind Kind { get; }
    ICustomAttributeProvider AttributeProvider { get; }
    bool IsByRef { get; }
    bool IsRequired { get; }
    bool IsNonNullable { get; }
    bool IsPublic { get; }
    bool HasDefaultValue { get; }
    object? DefaultValue { get; }
}

internal sealed class MethodParameterShapeInfo : IParameterShapeInfo
{
    public MethodParameterShapeInfo(ParameterInfo parameterInfo, bool isNonNullable, MemberInfo? matchingMember = null, string? logicalName = null, bool? isRequired = null)
    {
        string? name = logicalName ?? parameterInfo.Name;
        DebugExt.Assert(name != null);
        Name = name;

        Type = parameterInfo.GetEffectiveParameterType();
        ParameterInfo = parameterInfo;
        MatchingMember = matchingMember;
        IsNonNullable = isNonNullable;

        if (parameterInfo.TryGetDefaultValueNormalized(out object? defaultValue))
        {
            HasDefaultValue = true;
            DefaultValue = defaultValue;
        }

        IsRequired = isRequired ?? !ParameterInfo.HasDefaultValue;
    }

    public ParameterInfo ParameterInfo { get; }
    public MemberInfo? MatchingMember { get; }

    public Type Type { get; }
    public string Name { get; }
    public ParameterKind Kind => ParameterKind.MethodParameter;
    public ICustomAttributeProvider AttributeProvider => ParameterInfo;
    public bool IsByRef => ParameterInfo.ParameterType.IsByRef;
    public bool IsRequired { get; }
    public bool IsNonNullable { get; }
    public bool IsPublic => true;
    public bool HasDefaultValue { get; }
    public object? DefaultValue { get; }
}

internal sealed class MemberInitializerShapeInfo : IParameterShapeInfo
{
    public MemberInitializerShapeInfo(MemberInfo memberInfo, string? logicalName, bool ctorSetsRequiredMembers, bool isSetterNonNullable, bool? isRequiredByAttribute)
    {
        Debug.Assert(memberInfo is PropertyInfo or FieldInfo);

        Type = memberInfo.MemberType();
        Name = logicalName ?? memberInfo.Name;
        MemberInfo = memberInfo;
        IsRequired = isRequiredByAttribute ?? (!ctorSetsRequiredMembers && memberInfo.IsRequired());
        IsInitOnly = memberInfo.IsInitOnly();
        IsPublic = memberInfo is FieldInfo { IsPublic: true } or PropertyInfo { GetMethod.IsPublic: true };
        IsNonNullable = isSetterNonNullable;
    }

    public Type Type { get; }
    public MemberInfo MemberInfo { get; }
    public bool IsByRef => false;
    public bool IsRequired { get; }
    public bool IsInitOnly { get; }
    public bool IsNonNullable { get; }
    public bool IsPublic { get; }

    public string Name { get; }
    public ICustomAttributeProvider AttributeProvider => MemberInfo;
    public bool HasDefaultValue => false;
    public object? DefaultValue => null;
    public ParameterKind Kind => ParameterKind.MemberInitializer;
}