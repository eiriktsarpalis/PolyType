using System.Diagnostics;
using System.Reflection;
using PolyType.Abstractions;

namespace PolyType.ReflectionProvider;

internal sealed class ReflectionPropertyShape<TDeclaringType, TPropertyType> : IPropertyShape<TDeclaringType, TPropertyType>
{
    private readonly object _syncObject = new();
    private readonly ReflectionTypeShapeProvider _provider;
    private readonly MemberInfo _memberInfo;
    private readonly MemberInfo[]? _parentMembers; // stack of parent members reserved for nested tuple representations

    private Getter<TDeclaringType, TPropertyType>? _getter;
    private Setter<TDeclaringType, TPropertyType>? _setter;

    public ReflectionPropertyShape(ReflectionTypeShapeProvider provider, IObjectTypeShape<TDeclaringType> declaringType, PropertyShapeInfo shapeInfo)
        : base(provider)
    {
        Debug.Assert(shapeInfo.MemberInfo.DeclaringType!.IsAssignableFrom(typeof(TDeclaringType)) || shapeInfo.ParentMembers is not null);
        Debug.Assert(shapeInfo.MemberInfo is PropertyInfo or FieldInfo);
        Debug.Assert(shapeInfo.ParentMembers is null || typeof(TDeclaringType).IsNestedTupleRepresentation());

        _provider = provider;
        _memberInfo = shapeInfo.MemberInfo;
        _parentMembers = shapeInfo.ParentMembers;
        DeclaringType = declaringType;
        AttributeProvider = shapeInfo.AttributeProvider;

        Name = shapeInfo.LogicalName ?? shapeInfo.MemberInfo.Name;

        if (shapeInfo.MemberInfo is FieldInfo f)
        {
            HasGetter = true;
            HasSetter = !f.IsInitOnly;
            IsField = true;
            IsGetterPublic = f.IsPublic;
            IsSetterPublic = !f.IsInitOnly && f.IsPublic;
        }
        else
        {
            PropertyInfo p = (PropertyInfo)shapeInfo.MemberInfo;
            HasGetter = p.CanRead && (shapeInfo.IncludeNonPublicAccessors || p.GetMethod!.IsPublic);
            HasSetter = p.CanWrite && (shapeInfo.IncludeNonPublicAccessors || p.SetMethod!.IsPublic) && !p.IsInitOnly();
            IsGetterPublic = HasGetter && p.GetMethod!.IsPublic;
            IsSetterPublic = HasSetter && p.SetMethod!.IsPublic;
        }

        IsGetterNonNullable = HasGetter && shapeInfo.IsGetterNonNullable;
        IsSetterNonNullable = HasSetter && shapeInfo.IsSetterNonNullable;
    }

    public override string Name { get; }
    public override ICustomAttributeProvider AttributeProvider { get; }
    public override IObjectTypeShape<TDeclaringType> DeclaringType { get; }
    public override ITypeShape<TPropertyType> PropertyType => _provider.GetShape<TPropertyType>();

    public override bool IsField { get; }
    public override bool IsGetterPublic { get; }
    public override bool IsSetterPublic { get; }
    public override bool IsGetterNonNullable { get; }
    public override bool IsSetterNonNullable { get; }

    public override bool HasGetter { get; }
    public override bool HasSetter { get; }

    public override Getter<TDeclaringType, TPropertyType> GetGetter()
    {
        if (!HasGetter)
        {
            Throw();
            static void Throw() => throw new InvalidOperationException("The current property shape does not define a getter.");
        }

        return _getter ?? Helper();

        Getter<TDeclaringType, TPropertyType> Helper()
        {
            lock (_syncObject)
            {
                return _getter ??= _provider.MemberAccessor.CreateGetter<TDeclaringType, TPropertyType>(_memberInfo, _parentMembers);
            }
        }
    }

    public override Setter<TDeclaringType, TPropertyType> GetSetter()
    {
        if (!HasSetter)
        {
            Throw();
            static void Throw() => throw new InvalidOperationException("The current property shape does not define a setter.");
        }

        return _setter ?? Helper();

        Setter<TDeclaringType, TPropertyType> Helper()
        {
            lock (_syncObject)
            {
                return _setter ??= _provider.MemberAccessor.CreateSetter<TDeclaringType, TPropertyType>(_memberInfo, _parentMembers);
            }
        }
    }
}

internal sealed record PropertyShapeInfo(
    Type DeclaringType,
    MemberInfo MemberInfo,
    ICustomAttributeProvider AttributeProvider,
    MemberInfo[]? ParentMembers = null,
    string? LogicalName = null,
    int Order = 0,
    bool? IsRequiredByAttribute = null,
    bool IncludeNonPublicAccessors = false,
    bool IsGetterNonNullable = false,
    bool IsSetterNonNullable = false);