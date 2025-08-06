namespace PolyType.ReflectionProvider;

/// <summary>
/// A model that captures a type shape options as resolved
/// from <see cref="TypeShapeAttribute"/> and <see cref="TypeShapeExtensionAttribute"/> declarations.
/// </summary>
internal sealed class ReflectionTypeShapeOptions
{
    /// <inheritdoc cref="TypeShapeExtensionAttribute.Kind"/>/>
    public required TypeShapeKind? RequestedKind { get; init; }

    /// <inheritdoc cref="TypeShapeExtensionAttribute.Marshaller" />
    public required Type? Marshaller { get; init; }

    /// <inheritdoc cref="TypeShapeExtensionAttribute.IncludeMethods" />
    public required MethodShapeFlags IncludeMethods { get; init; }
}
