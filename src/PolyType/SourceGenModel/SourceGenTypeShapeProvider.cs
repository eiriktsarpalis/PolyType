using PolyType.Abstractions;

namespace PolyType.SourceGenModel;

/// <summary>
/// Defines a source generated <see cref="ITypeShapeProvider"/> implementation.
/// </summary>
public abstract class SourceGenTypeShapeProvider : ITypeShapeProvider
{
    /// <summary>
    /// Gets a <see cref="ITypeShape"/> instance corresponding to the supplied type.
    /// </summary>
    /// <param name="type">The type for which a shape is requested.</param>
    /// <returns>
    /// A <see cref="ITypeShape"/> instance corresponding to the current type.
    /// </returns>
    public abstract ITypeShape? GetTypeShape(Type type);
}
