using PolyType.Abstractions;

namespace PolyType.SourceGenModel;

/// <summary>
/// Defines a source generated <see cref="ITypeShapeProvider"/> implementation.
/// </summary>
public abstract class SourceGenTypeShapeProvider : ITypeShapeProvider
{
    /// <summary>
    /// Gets the informational version of the source generator that created this provider.
    /// </summary>
    /// <remarks>
    /// A <see langword="null"/> value typically identifies legacy generated output.
    /// </remarks>
    public string? SourceGeneratorVersion { get; init; }

    /// <summary>
    /// Gets the callback used to infer contextual status for legacy generated shapes.
    /// </summary>
    /// <remarks>
    /// Current generated providers override this property to return <see langword="null"/>,
    /// allowing the legacy type lookup and unused generated shapes to be trimmed.
    /// </remarks>
    protected internal virtual Func<ITypeShape, bool>? IsLegacyShapeContextual =>
        shape => !ReferenceEquals(shape, GetTypeShape(shape.Type));

    /// <summary>
    /// Gets a <see cref="ITypeShape"/> instance corresponding to the supplied type.
    /// </summary>
    /// <param name="type">The type for which a shape is requested.</param>
    /// <returns>
    /// A <see cref="ITypeShape"/> instance corresponding to the current type.
    /// </returns>
    public abstract ITypeShape? GetTypeShape(Type type);

    /// <summary>
    /// Infers contextual status for shapes created by legacy generated output.
    /// </summary>
    /// <param name="shape">The shape whose contextual status is being resolved.</param>
    /// <returns>Whether the shape differs from the legacy provider's ordinary representation.</returns>
    internal bool IsShapeContextual(ITypeShape shape) =>
        SourceGeneratorVersion is null && IsLegacyShapeContextual?.Invoke(shape) is true;
}
