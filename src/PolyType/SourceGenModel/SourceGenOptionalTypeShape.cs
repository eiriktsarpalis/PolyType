using PolyType.Abstractions;

namespace PolyType.SourceGenModel;

/// <summary>
/// Source generator model for nullable types.
/// </summary>
/// <typeparam name="TOptional">The optional type described by the shape.</typeparam>
/// <typeparam name="TElement">The type of the value encapsulated by the option type.</typeparam>
public sealed class SourceGenOptionalTypeShape<TOptional, TElement> : SourceGenTypeShape<TOptional>, IOptionalTypeShape<TOptional, TElement>
{
    /// <inheritdoc/>
    public required ITypeShape<TElement> ElementType { get; init; }

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

    /// <inheritdoc/>
    public override TypeShapeKind Kind => TypeShapeKind.Optional;

    /// <inheritdoc/>
    public override object? Accept(TypeShapeVisitor visitor, object? state = null) => visitor.VisitOptional(this, state);

    Func<TOptional> IOptionalTypeShape<TOptional, TElement>.GetNoneConstructor() => NoneConstructor;
    Func<TElement, TOptional> IOptionalTypeShape<TOptional, TElement>.GetSomeConstructor() => SomeConstructor;
    OptionDeconstructor<TOptional, TElement> IOptionalTypeShape<TOptional, TElement>.GetDeconstructor() => Deconstructor;
    ITypeShape IOptionalTypeShape.ElementType => ElementType;
}
