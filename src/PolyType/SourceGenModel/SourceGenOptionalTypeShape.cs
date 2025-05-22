using PolyType.Abstractions;

namespace PolyType.SourceGenModel;

/// <summary>
/// Source generator model for nullable types.
/// </summary>
/// <typeparam name="TOptional">The optional type described by the shape.</typeparam>
/// <typeparam name="TElement">The type of the value encapsulated by the option type.</typeparam>
public sealed class SourceGenOptionalTypeShape<TOptional, TElement>(ITypeShapeProvider provider) : IOptionalTypeShape<TOptional, TElement>(provider)
{
    /// <summary>
    /// Gets the shape of the element type.
    /// </summary>
    public required ITypeShape<TElement> ElementTypeSetter { private get; init; }

    /// <inheritdoc/>
    public override ITypeShape<TElement> ElementType => this.ElementTypeSetter;

    /// <summary>
    /// Gets a constructor for creating empty instances of <typeparamref name="TOptional"/>.
    /// </summary>
    public required Func<TOptional> NoneConstructor { get; init; }

    /// <summary>
    /// Gets a constructor for creating populated instances of <typeparamref name="TOptional"/>.
    /// </summary>
    public required Func<TElement, TOptional> SomeConstructor { get; init; }

    /// <summary>
    /// Gets a deconstructor delegate for <typeparamref name="TOptional"/> instances.
    /// </summary>
    public required OptionDeconstructor<TOptional, TElement> Deconstructor { get; init; }

    /// <summary>
    /// Gets the shape of an associated type, by its name.
    /// </summary>
    public Func<string, ITypeShape?>? AssociatedTypeShapes { get; init; }

    /// <inheritdoc/>
    public override Func<TOptional> GetNoneConstructor() => NoneConstructor;

    /// <inheritdoc/>
    public override Func<TElement, TOptional> GetSomeConstructor() => SomeConstructor;

    /// <inheritdoc/>
    public override OptionDeconstructor<TOptional, TElement> GetDeconstructor() => Deconstructor;

    /// <inheritdoc/>
    public override ITypeShape? GetAssociatedTypeShape(Type associatedType) => InternalTypeShapeExtensions.GetAssociatedTypeShape(this, AssociatedTypeShapes, associatedType);
}
