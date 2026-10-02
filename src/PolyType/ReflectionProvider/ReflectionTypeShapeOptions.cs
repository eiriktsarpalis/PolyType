namespace PolyType.ReflectionProvider;

/// <summary>
/// Captures type shape configuration, including options resolved
/// from <see cref="TypeShapeAttribute"/> and <see cref="TypeShapeExtensionAttribute"/> declarations.
/// </summary>
internal sealed class ReflectionTypeShapeOptions
{
    public bool IsContextual { get; init; }

    /// <inheritdoc cref="TypeShapeExtensionAttribute.Kind"/>/>
    public required TypeShapeKind? RequestedKind { get; init; }

    /// <inheritdoc cref="TypeShapeExtensionAttribute.Marshaler" />
    public required Type? Marshaler { get; init; }

    /// <inheritdoc cref="TypeShapeExtensionAttribute.IncludeMethods" />
    public required MethodShapeFlags IncludeMethods { get; init; }

    /// <inheritdoc cref="TypeShapeAttribute.InferClosedTypePolymorphism" />
    public bool InferClosedTypePolymorphism { get; init; }
}
