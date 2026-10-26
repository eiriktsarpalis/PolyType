using PolyType.Abstractions;

namespace PolyType.SourceGenModel;

/// <summary>
/// Defines a source generated <see cref="ITypeShapeProvider"/> implementation.
/// </summary>
public abstract class SourceGenTypeShapeProvider : ITypeShapeProvider
{
    private readonly Func<Type, ITypeShape?>? _legacyTypeLookup;

    /// <summary>
    /// Initializes a new instance of the <see cref="SourceGenTypeShapeProvider"/> class for legacy generated output.
    /// </summary>
    protected SourceGenTypeShapeProvider()
    {
        _legacyTypeLookup = GetTypeShape;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SourceGenTypeShapeProvider"/> class for generated output that explicitly identifies contextual shapes.
    /// </summary>
    /// <param name="sourceGeneratorVersion">The informational version of the source generator.</param>
    /// <remarks>
    /// Shapes created by this provider must explicitly set <see cref="ITypeShape.IsContextual"/> when contextual.
    /// This constructor avoids registering the legacy type lookup used to infer contextual status,
    /// allowing unused generated shapes to be trimmed.
    /// </remarks>
    protected SourceGenTypeShapeProvider(string sourceGeneratorVersion)
    {
        SourceGeneratorVersion = sourceGeneratorVersion;
    }

    /// <summary>
    /// Gets the informational version of the source generator that created this provider.
    /// </summary>
    /// <remarks>
    /// A <see langword="null"/> value typically identifies legacy generated output.
    /// </remarks>
    public string? SourceGeneratorVersion { get; init; }

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
        SourceGeneratorVersion is null && _legacyTypeLookup is { } lookup &&
        !ReferenceEquals(shape, lookup(shape.Type));
}
