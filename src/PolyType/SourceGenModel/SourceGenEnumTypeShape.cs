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
    /// <inheritdoc/>
    public required ITypeShape<TUnderlying> UnderlyingType { get; init; }

    /// <inheritdoc/>
    public override TypeShapeKind Kind => TypeShapeKind.Enum;

    /// <inheritdoc/>
    public required IReadOnlyDictionary<string, TUnderlying> Members { get; init; }

    /// <inheritdoc/>
    public override object? Accept(TypeShapeVisitor visitor, object? state = null) => visitor.VisitEnum(this, state);

    ITypeShape IEnumTypeShape.UnderlyingType => UnderlyingType;
}
