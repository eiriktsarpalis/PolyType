using PolyType.Abstractions;
using System.Diagnostics;
using System.Reflection;

namespace PolyType.ReflectionProvider;

[DebuggerTypeProxy(typeof(PolyType.Debugging.ParameterShapeDebugView))]
[DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed class ReflectionParameterShape<TArgumentState, TParameter> : IParameterShape<TArgumentState, TParameter>
    where TArgumentState : IArgumentState
{
    private readonly ReflectionTypeShapeProvider _provider;
    private readonly IMethodShapeInfo _ctorInfo;
    private readonly IParameterShapeInfo _parameterInfo;
    private Getter<TArgumentState, TParameter>? _getter;
    private Setter<TArgumentState, TParameter>? _setter;

    public ReflectionParameterShape(
        ReflectionTypeShapeProvider provider,
        IMethodShapeInfo ctorInfo,
        IParameterShapeInfo parameterInfo,
        int position)
    {
        Debug.Assert(position < ctorInfo.Parameters.Length);

        _ctorInfo = ctorInfo;
        _parameterInfo = parameterInfo;
        Position = position;
        _provider = provider;
    }

    public ITypeShape<TParameter> ParameterType => _provider.GetTypeShape<TParameter>();

    public int Position { get; }
    public string Name => _parameterInfo.Name;
    public ParameterKind Kind => _parameterInfo.Kind;
    public bool HasDefaultValue => _parameterInfo.HasDefaultValue;
    public bool IsRequired => _parameterInfo.IsRequired;
    public bool IsNonNullable => _parameterInfo.IsNonNullable;
    public bool IsPublic => _parameterInfo.IsPublic;
    public TParameter? DefaultValue => (TParameter?)_parameterInfo.DefaultValue;
    object? IParameterShape.DefaultValue => _parameterInfo.DefaultValue;
    public ICustomAttributeProvider? AttributeProvider => _parameterInfo.AttributeProvider;
    ParameterInfo? IParameterShape.ParameterInfo => (_parameterInfo as MethodParameterShapeInfo)?.ParameterInfo;
    ITypeShape IParameterShape.ParameterType => ParameterType;
    object? IParameterShape.Accept(TypeShapeVisitor visitor, object? state) => visitor.VisitParameter(this, state);

    public Getter<TArgumentState, TParameter> GetGetter()
    {
        return _getter ?? CommonHelpers.ExchangeIfNull(ref _getter, CreateGetter());
        Getter<TArgumentState, TParameter> CreateGetter() =>
            _provider.MemberAccessor.CreateArgumentStateGetter<TArgumentState, TParameter>(_ctorInfo, Position);
    }

    public Setter<TArgumentState, TParameter> GetSetter()
    {
        return _setter ?? CommonHelpers.ExchangeIfNull(ref _setter, CreateSetter());
        Setter<TArgumentState, TParameter> CreateSetter() =>
            _provider.MemberAccessor.CreateArgumentStateSetter<TArgumentState, TParameter>(_ctorInfo, Position);
    }

    private string DebuggerDisplay => $"{{Type = \"{typeof(TParameter)}\", Name = \"{Name}\"}}";
}