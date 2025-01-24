using PolyType.Abstractions;

namespace PolyType;

/// <summary>
/// Defines a provider for <see cref="ITypeShape"/> implementations.
/// </summary>
public interface ITypeShapeProvider
{
    /// <summary>
    /// Gets a <see cref="ITypeShape"/> instance corresponding to the supplied type.
    /// </summary>
    /// <param name="type">The type for which a shape is requested.</param>
    /// <returns>
    /// A <see cref="ITypeShape"/> instance corresponding to the current type,
    /// or <see langword="null" /> if a shape is not available.
    /// </returns>
    ITypeShape? GetShape(Type type);

    /// <summary>
    /// Gets a <see cref="ITypeShape"/> instance corresponding to a closed generic type.
    /// </summary>
    /// <param name="unboundGenericType">The open generic type (e.g. <c>IGeneric&lt;&gt;</c>).</param>
    /// <param name="genericTypeArguments">The type arguments used to close the <paramref name="unboundGenericType"/>.</param>
    /// <returns>The shape of the closed generic type, if one can be found or created.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown if <paramref name="unboundGenericType"/> is not an open generic type
    /// or <paramref name="genericTypeArguments"/> does not have an appropriate number of type arguments
    /// to close the generic type, or the type arguments do not satisfy the applicable type constraints.
    /// </exception>
    ITypeShape? GetShape(Type unboundGenericType, ReadOnlySpan<Type> genericTypeArguments);
}
