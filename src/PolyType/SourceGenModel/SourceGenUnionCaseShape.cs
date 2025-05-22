using PolyType.Abstractions;

namespace PolyType.SourceGenModel;

/// <summary>
/// Source generator model for union case shapes.
/// </summary>
/// <typeparam name="TUnionCase">The type of the current union case.</typeparam>
/// <typeparam name="TUnion">The type of the base union type.</typeparam>
public sealed class SourceGenUnionCaseShape<TUnionCase, TUnion> : IUnionCaseShape<TUnionCase, TUnion>
    where TUnionCase : TUnion
{
    /// <summary>
    /// Gets the underlying type shape of the union case.
    /// </summary>
    public required ITypeShape<TUnionCase> TypeSetter { get; init; }

    /// <inheritdoc/>
    public override ITypeShape<TUnionCase> Type => TypeSetter;

    /// <summary>
    /// Gets the unique string identifier for the current union case.
    /// </summary>
    public required string NameSetter { get; init; }

    /// <inheritdoc/>
    public override string Name => NameSetter;

    /// <summary>
    /// Gets the unique integer identifier for the current union case.
    /// </summary>
    public required int TagSetter { get; init; }

    /// <inheritdoc/>
    public override int Tag => TagSetter;

    /// <inheritdoc cref="IsTagSpecified"/>
    public required bool IsTagSpecifiedSetter { get; init; }

    /// <inheritdoc/>
    public override bool IsTagSpecified => IsTagSpecifiedSetter;

    /// <summary>
    /// Gets the unique index corresponding to the current union case.
    /// </summary>
    public required int IndexSetter { get; init; }

    /// <inheritdoc/>
    public override int Index => IndexSetter;
}
