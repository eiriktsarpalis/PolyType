using PolyType.Abstractions;

namespace PolyType.SourceGenModel;

/// <summary>
/// Source generator model for enum shapes.
/// </summary>
/// <typeparam name="TEnum">The type of the enum.</typeparam>
/// <typeparam name="TUnderlying">The type of the underlying type of the enum.</typeparam>
public sealed class SourceGenEnumTypeShape<TEnum, TUnderlying>(SourceGenTypeShapeProvider provider) : IEnumTypeShape<TEnum, TUnderlying>(provider)
    where TEnum : struct, Enum
{
    /// <summary>
    /// Gets the shape of the underlying type of the enum.
    /// </summary>
    public required ITypeShape<TUnderlying> UnderlyingTypeSetter { private get; init; }

    /// <inheritdoc/>
    public override ITypeShape<TUnderlying> UnderlyingType => this.UnderlyingTypeSetter;

    /// <summary>
    /// Gets the shape of an associated type, by its name.
    /// </summary>
    public Func<string, ITypeShape?>? AssociatedTypeShapes { get; init; }

    /// <inheritdoc/>
    public override ITypeShape? GetAssociatedTypeShape(Type associatedType) => InternalTypeShapeExtensions.GetAssociatedTypeShape(this, AssociatedTypeShapes, associatedType);
}
