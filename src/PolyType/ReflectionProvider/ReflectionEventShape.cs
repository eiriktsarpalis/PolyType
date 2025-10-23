using PolyType.Abstractions;
using System.Diagnostics;
using System.Reflection;

namespace PolyType.ReflectionProvider;

[DebuggerTypeProxy(typeof(PolyType.Debugging.EventShapeDebugView))]
[DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed class ReflectionEventShape<TDeclaringType, TEventHandler> : IEventShape<TDeclaringType, TEventHandler>
{
    private readonly ReflectionTypeShapeProvider _provider;
    private readonly EventInfo _eventInfo;
    private IFunctionTypeShape? _handlerType;
    private Setter<TDeclaringType?, TEventHandler>? _addHandler;
    private Setter<TDeclaringType?, TEventHandler>? _removeHandler;

    public ReflectionEventShape(EventInfo eventInfo, string name, ReflectionTypeShapeProvider provider)
    {
        DebugExt.Assert(eventInfo.AddMethod is not null && eventInfo.RemoveMethod is not null);
        _eventInfo = eventInfo;
        _provider = provider;
        Name = name;
    }

    public string Name { get; }
    public bool IsStatic => _eventInfo.AddMethod!.IsStatic;
    public bool IsPublic => _eventInfo.AddMethod!.IsPublic;
    public ITypeShape<TDeclaringType> DeclaringType => _provider.GetTypeShape<TDeclaringType>();
    public IFunctionTypeShape HandlerType => _handlerType ?? CommonHelpers.ExchangeIfNull(ref _handlerType, (IFunctionTypeShape)_provider.GetTypeShape<TEventHandler>());
    public ICustomAttributeProvider? AttributeProvider => _eventInfo;
    EventInfo? IEventShape.EventInfo => _eventInfo;
    public object? Accept(TypeShapeVisitor visitor, object? state = null) => visitor.VisitEvent(this, state);
    ITypeShape IEventShape.DeclaringType => DeclaringType;

    public Setter<TDeclaringType?, TEventHandler> GetAddHandler()
    {
        return _addHandler ?? CommonHelpers.ExchangeIfNull(ref _addHandler, _provider.MemberAccessor.CreateEventAccessor<TDeclaringType, TEventHandler>(_eventInfo.AddMethod!));
    }

    public Setter<TDeclaringType?, TEventHandler> GetRemoveHandler()
    {
        return _removeHandler ?? CommonHelpers.ExchangeIfNull(ref _removeHandler, _provider.MemberAccessor.CreateEventAccessor<TDeclaringType, TEventHandler>(_eventInfo.RemoveMethod!));
    }

    private string DebuggerDisplay => $"{{Name = \"{Name}\", Handler = \"{typeof(TEventHandler)}\"}}";
}
