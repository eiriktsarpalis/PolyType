namespace PolyType.ReflectionProvider;

/// <summary>
/// A model that captures a type shape options as resolved
/// from <see cref="TypeShapeAttribute"/> and <see cref="TypeShapeExtensionAttribute"/> declarations.
/// </summary>
internal sealed class ReflectionTypeShapeOptions
{
    /// <inheritdoc cref="TypeShapeExtensionAttribute.Kind"/>/>
    public required TypeShapeKind? RequestedKind { get; init; }

    /// <inheritdoc cref="TypeShapeExtensionAttribute.Marshaler" />
    public required Type? Marshaler { get; init; }

    /// <inheritdoc cref="TypeShapeExtensionAttribute.IncludeMethods" />
    public required MethodShapeFlags IncludeMethods { get; init; }

    /// <summary>
    /// Indicates whether derived types should be inferred from the type hierarchy.
    /// </summary>
    public bool InferDerivedTypes { get; init; }
}
