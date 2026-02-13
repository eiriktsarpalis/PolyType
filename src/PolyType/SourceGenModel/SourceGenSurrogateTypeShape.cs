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

    /// <summary>
    /// Gets a delayed surrogate type shape factory for use with potentially recursive type graphs.
    /// </summary>
    public required Func<ITypeShape<TSurrogate>> SurrogateTypeFunc { get; init; }

    /// <inheritdoc/>
    [Obsolete("This member has been marked for deprecation and will be removed in the future.")]
    public ITypeShape<TSurrogate> SurrogateType
    {
        get => field ??= SurrogateTypeFunc.Invoke();
        init;
    }

    /// <inheritdoc/>
    public override TypeShapeKind Kind => TypeShapeKind.Surrogate;

    /// <inheritdoc/>
    public override object? Accept(TypeShapeVisitor visitor, object? state = null) => visitor.VisitSurrogate(this, state);

#pragma warning disable CS0618 // Type or member is obsolete
    ITypeShape ISurrogateTypeShape.SurrogateType => SurrogateType;
#pragma warning restore CS0618
}