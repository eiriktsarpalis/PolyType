using PolyType.Abstractions;
using System.Diagnostics;
using System.Reflection;

namespace PolyType.ReflectionProvider;

[DebuggerTypeProxy(typeof(PolyType.Debugging.PropertyShapeDebugView))]
[DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed class ReflectionPropertyShape<TDeclaringType, TPropertyType> : IPropertyShape<TDeclaringType, TPropertyType>
{
    private readonly ReflectionTypeShapeProvider _provider;
    private readonly MemberInfo _baseMemberInfo;
    private readonly MemberInfo[]? _parentMembers; // stack of parent members reserved for nested tuple representations

    private Getter<TDeclaringType, TPropertyType>? _getter;
    private Setter<TDeclaringType, TPropertyType>? _setter;

    public ReflectionPropertyShape(ReflectionTypeShapeProvider provider, IObjectTypeShape<TDeclaringType> declaringType, PropertyShapeInfo shapeInfo, int position)
    {
        Debug.Assert(shapeInfo.BaseMemberInfo.DeclaringType!.IsAssignableFrom(typeof(TDeclaringType)) || shapeInfo.ParentMembers is not null);
        Debug.Assert(shapeInfo.BaseMemberInfo is PropertyInfo or FieldInfo);
        Debug.Assert(shapeInfo.ParentMembers is null || typeof(TDeclaringType).IsNestedTupleRepresentation());

        _provider = provider;
        _parentMembers = shapeInfo.ParentMembers;
        _baseMemberInfo = shapeInfo.BaseMemberInfo;
        DeclaringType = declaringType;
        MemberInfo = shapeInfo.DerivedMemberInfo;

        Position = position;
        Name = shapeInfo.LogicalName ?? shapeInfo.BaseMemberInfo.Name;

        if (shapeInfo.BaseMemberInfo is FieldInfo f)
        {
            HasGetter = true;
            HasSetter = !f.IsInitOnly;
            IsField = true;
            IsGetterPublic = f.IsPublic;
            IsSetterPublic = !f.IsInitOnly && f.IsPublic;
        }
        else
        {
            PropertyInfo p = (PropertyInfo)shapeInfo.BaseMemberInfo;
            HasGetter = p.CanRead && (shapeInfo.IncludeNonPublicAccessors || p.GetMethod!.IsPublic);
            HasSetter = p.CanWrite && (shapeInfo.IncludeNonPublicAccessors || p.SetMethod!.IsPublic) && !p.IsInitOnly();
            IsGetterPublic = HasGetter && p.GetMethod!.IsPublic;
            IsSetterPublic = HasSetter && p.SetMethod!.IsPublic;
        }

        IsGetterNonNullable = HasGetter && shapeInfo.IsGetterNonNullable;
        IsSetterNonNullable = HasSetter && shapeInfo.IsSetterNonNullable;
    }

    public int Position { get; }
    public string Name { get; }
    public MemberInfo MemberInfo { get; }

    public IGenericCustomAttributeProvider AttributeProvider => _attributeProvider ?? CommonHelpers.ExchangeIfNull(ref _attributeProvider, new(MemberInfo));
    private ReflectionCustomAttributeProvider? _attributeProvider;

    public IObjectTypeShape<TDeclaringType> DeclaringType { get; }
    public ITypeShape<TPropertyType> PropertyType => _provider.GetTypeShape<TPropertyType>();

    public bool IsField { get; }
    public bool IsGetterPublic { get; }
    public bool IsSetterPublic { get; }
    public bool IsGetterNonNullable { get; }
    public bool IsSetterNonNullable { get; }

    public bool HasGetter { get; }
    public bool HasSetter { get; }

    IObjectTypeShape IPropertyShape.DeclaringType => DeclaringType;
    ITypeShape IPropertyShape.PropertyType => PropertyType;
    object? IPropertyShape.Accept(TypeShapeVisitor visitor, object? state) => visitor.VisitProperty(this, state);

    public Getter<TDeclaringType, TPropertyType> GetGetter()
    {
        if (!HasGetter)
        {
            Throw();
            static void Throw() => throw new InvalidOperationException("The current property shape does not define a getter.");
        }

        return _getter ?? CommonHelpers.ExchangeIfNull(ref _getter, CreateGetter());

        Getter<TDeclaringType, TPropertyType> CreateGetter() =>
            _provider.MemberAccessor.CreateGetter<TDeclaringType, TPropertyType>(_baseMemberInfo, _parentMembers);
    }

    public Setter<TDeclaringType, TPropertyType> GetSetter()
    {
        if (!HasSetter)
        {
            Throw();
            static void Throw() => throw new InvalidOperationException("The current property shape does not define a setter.");
        }

        return _setter ?? CommonHelpers.ExchangeIfNull(ref _setter, CreateSetter());

        Setter<TDeclaringType, TPropertyType> CreateSetter() =>
            _provider.MemberAccessor.CreateSetter<TDeclaringType, TPropertyType>(_baseMemberInfo, _parentMembers);
    }

    private string DebuggerDisplay => $"{{Type = \"{typeof(TPropertyType)}\", Name = \"{Name}\"}}";
}

internal sealed record PropertyShapeInfo(
    Type DeclaringType,
    MemberInfo BaseMemberInfo,
    MemberInfo DerivedMemberInfo,
    MemberInfo[]? ParentMembers = null,
    string? LogicalName = null,
    int Order = 0,
    bool? IsRequiredByAttribute = null,
    bool IncludeNonPublicAccessors = false,
    bool IsGetterNonNullable = false,
    bool IsSetterNonNullable = false);