namespace PolyType.Abstractions;

/// <summary>
/// Provides a strongly typed shape model for optional types.
/// </summary>
/// <remarks>
/// Examples of optional types include <see cref="Nullable{T}"/> or the F# option types.
/// </remarks>
public interface IOptionalTypeShape : ITypeShape
{
    /// <summary>
    /// Gets the shape of underlying value type.
    /// </summary>
    ITypeShape ElementType { get; }
}

/// <summary>
/// Provides a strongly typed shape model for optional types.
/// </summary>
/// <typeparam name="TOptional">The optional type described by the shape.</typeparam>
/// <typeparam name="TElement">The value encapsulated by the option type.</typeparam>
/// <remarks>
/// Examples of optional types include <see cref="Nullable{T}"/> or the F# option types.
/// </remarks>
public interface IOptionalTypeShape<TOptional, TElement> : ITypeShape<TOptional>, IOptionalTypeShape
{
    /// <summary>
    /// Gets the shape of the underlying value type.
    /// </summary>
    new ITypeShape<TElement> ElementType { get; }

    /// <summary>
    /// Gets a constructor for creating empty (aka 'None') instances of <typeparamref name="TOptional"/>.
    /// </summary>
    /// <returns>A delegate for creating empty (aka 'None') instances of <typeparamref name="TOptional"/>.</returns>
    Func<TOptional> GetNoneConstructor();

    /// <summary>
    /// Gets a constructor for creating populated (aka 'Some') instances of <typeparamref name="TOptional"/>.
    /// </summary>
    /// <returns>A delegate for creating populated (aka 'Some') instances of <typeparamref name="TOptional"/>.</returns>
    Func<TElement, TOptional> GetSomeConstructor();

    /// <summary>
    /// Gets a deconstructor delegate for <typeparamref name="TOptional"/> instances.
    /// </summary>
    /// <returns>A delegate for deconstructing <typeparamref name="TOptional"/> instances.</returns>
    OptionDeconstructor<TOptional, TElement> GetDeconstructor();
}