using PolyType.Abstractions;
using System.Diagnostics;

namespace PolyType.SourceGenModel;

/// <summary>
/// Source generator model for enum shapes.
/// </summary>
/// <typeparam name="TEnum">The type of the enum.</typeparam>
/// <typeparam name="TUnderlying">The type of the underlying type of the enum.</typeparam>
[DebuggerTypeProxy(typeof(PolyType.Debugging.EnumTypeShapeDebugView))]
public sealed class SourceGenEnumTypeShape<TEnum, TUnderlying> : SourceGenTypeShape<TEnum>, IEnumTypeShape<TEnum, TUnderlying>
    where TEnum : struct, Enum
    where TUnderlying : unmanaged
{
    /// <summary>
    /// Gets a delayed underlying type shape factory for use with potentially recursive type graphs.
    /// </summary>
    public required Func<ITypeShape<TUnderlying>> UnderlyingTypeFactory { get; init; }

    /// <inheritdoc/>
    [Obsolete("This member has been marked for deprecation and will be removed in the future.")]
    public ITypeShape<TUnderlying> UnderlyingType
    {
        get => field ??= UnderlyingTypeFactory.Invoke();
        init;
    }

    /// <inheritdoc/>
    public override TypeShapeKind Kind => TypeShapeKind.Enum;

    /// <inheritdoc/>
    public required IReadOnlyDictionary<string, TUnderlying> Members { get; init; }

    /// <inheritdoc/>
    public bool IsFlags { get; init; }

    /// <inheritdoc/>
    public override object? Accept(TypeShapeVisitor visitor, object? state = null) => visitor.VisitEnum(this, state);

#pragma warning disable CS0618 // Type or member is obsolete
    ITypeShape IEnumTypeShape.UnderlyingType => UnderlyingType;
#pragma warning restore CS0618
}
