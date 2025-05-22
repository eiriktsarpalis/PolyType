using PolyType.Abstractions;

namespace PolyType.SourceGenModel;

/// <summary>
/// Source generator model for union type shapes.
/// </summary>
/// <typeparam name="TUnion">The type whose shape is described.</typeparam>
public sealed class SourceGenUnionTypeShape<TUnion>(SourceGenTypeShapeProvider provider) : IUnionTypeShape<TUnion>(provider)
{
    /// <summary>
    /// Gets the underlying type shape of the union base type.
    /// </summary>
    public required ITypeShape<TUnion> BaseTypeSetter { private get; init; }

    /// <inheritdoc/>
    public override ITypeShape<TUnion> BaseType => BaseTypeSetter;

    /// <summary>
    /// Gets a factory method for creating union case shapes.
    /// </summary>
    public required Func<IEnumerable<IUnionCaseShape>> CreateUnionCasesFunc { get; init; }

    /// <summary>
    /// Gets a delegate that computes the union case index for a given value.
    /// </summary>
    public required Getter<TUnion, int> GetUnionCaseIndexFunc { get; init; }

    /// <inheritdoc/>
    public override IReadOnlyList<IUnionCaseShape> UnionCases => _unionCases ??= CreateUnionCasesFunc().AsReadOnlyList();
    private IReadOnlyList<IUnionCaseShape>? _unionCases;

    /// <summary>
    /// Gets the shape of an associated type, by its name.
    /// </summary>
    public Func<string, ITypeShape?>? AssociatedTypeShapes { get; init; }

    /// <inheritdoc/>
    public override ITypeShape? GetAssociatedTypeShape(Type associatedType) => InternalTypeShapeExtensions.GetAssociatedTypeShape(this, AssociatedTypeShapes, associatedType);

    /// <inheritdoc/>
    public override Getter<TUnion, int> GetGetUnionCaseIndex() => GetUnionCaseIndexFunc;
}
