using PolyType.Abstractions;
using System.Diagnostics;

namespace PolyType.SourceGenModel;

/// <summary>
/// Source generator model for surrogate type shapes.
/// </summary>
/// <typeparam name="T">The type that the shape describes.</typeparam>
/// <typeparam name="TSurrogate">The surrogate type used by the shape.</typeparam>
[DebuggerTypeProxy(typeof(PolyType.Debugging.SurrogateTypeShapeDebugView))]
public sealed class SourceGenSurrogateTypeShape<T, TSurrogate> : SourceGenTypeShape<T>, ISurrogateTypeShape<T, TSurrogate>
{
    /// <inheritdoc/>
    public required IMarshaler<T, TSurrogate> Marshaler { get; init; }

    /// <inheritdoc/>
    public required ITypeShape<TSurrogate> SurrogateType { get; init; }

    /// <inheritdoc/>
    public override TypeShapeKind Kind => TypeShapeKind.Surrogate;

    /// <inheritdoc/>
    public override object? Accept(TypeShapeVisitor visitor, object? state = null) => visitor.VisitSurrogate(this, state);

    ITypeShape ISurrogateTypeShape.SurrogateType => SurrogateType;
}