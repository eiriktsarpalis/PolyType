using PolyType.Abstractions;
using System.Diagnostics;
using System.Reflection;

namespace PolyType.SourceGenModel;

/// <summary>
/// Source generator model for event shapes.
/// </summary>
/// <typeparam name="TDeclaringType">The type declaring the event.</typeparam>
/// <typeparam name="TEventHandler">The type of the event handler.</typeparam>
[DebuggerTypeProxy(typeof(PolyType.Debugging.EventShapeDebugView))]
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class SourceGenEventShape<TDeclaringType, TEventHandler> : IEventShape<TDeclaringType, TEventHandler>
{
    /// <inheritdoc/>
    public required string Name { get; init; }

    /// <inheritdoc/>
    public required bool IsStatic { get; init; }

    /// <inheritdoc/>
    public required bool IsPublic { get; init; }

    /// <inheritdoc/>
    public required ITypeShape<TDeclaringType> DeclaringType { get; init; }

    /// <summary>
    /// Gets the shape of the event handler type.
    /// </summary>
    public required IFunctionTypeShape HandlerType { get; init; }

    /// <summary>
    /// Gets the delegate that adds an event handler to the event.
    /// </summary>
    public required Setter<TDeclaringType?, TEventHandler> AddHandler { get; init; }

    /// <summary>
    /// Gets the delegate that removes an event handler from the event.
    /// </summary>
    public required Setter<TDeclaringType?, TEventHandler> RemoveHandler { get; init; }

    /// <summary>
    /// Gets a constructor delegate for the custom attribute provider of the event.
    /// </summary>
    public Func<ICustomAttributeProvider?>? AttributeProviderFunc { get; init; }

    ITypeShape IEventShape.DeclaringType => DeclaringType;

    ICustomAttributeProvider? IEventShape.AttributeProvider => AttributeProviderFunc?.Invoke();

    object? IEventShape.Accept(TypeShapeVisitor visitor, object? state) => visitor.VisitEvent(this, state);

    Setter<TDeclaringType?, TEventHandler> IEventShape<TDeclaringType, TEventHandler>.GetAddHandler() => AddHandler;
    Setter<TDeclaringType?, TEventHandler> IEventShape<TDeclaringType, TEventHandler>.GetRemoveHandler() => RemoveHandler;

    private string DebuggerDisplay => $"{{Name = \"{Name}\", Handler = \"{typeof(TEventHandler)}\"}}";
}
