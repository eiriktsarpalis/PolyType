namespace PolyType.ReflectionProvider;

/// <summary>
/// A model that captures a type shape extension as described by a <see cref="TypeShapeExtensionAttribute"/>
/// as it applies to reflection-based type shape providers.
/// </summary>
internal readonly record struct ReflectionTypeShapeExtensionModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReflectionTypeShapeExtensionModel"/> struct.
    /// </summary>
    /// <param name="attribute">The attribute to initialize the model from.</param>
    internal ReflectionTypeShapeExtensionModel(TypeShapeExtensionAttribute attribute)
    {
        // The associated type properties on the attribute are not considered
        // because the reflection type shape providers can create associations with everything on demand.
        this.Marshaller = attribute.Marshaller;
    }

    /// <inheritdoc cref="TypeShapeExtensionAttribute.Marshaller" />
    internal Type? Marshaller { get; init; }

    /// <summary>
    /// Merges the content of this and another type shape extension.
    /// </summary>
    /// <param name="other">The other type shape extension.</param>
    /// <returns>The merged result.</returns>
    /// <remarks>
    /// Conflicts are resolved by preferring the values in <em>this</em> model.
    /// </remarks>
    internal ReflectionTypeShapeExtensionModel Merge(ReflectionTypeShapeExtensionModel other)
    {
        return this with
        {
            Marshaller = Marshaller ?? other.Marshaller,
        };
    }
}
