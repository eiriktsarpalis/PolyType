namespace PolyType.Abstractions;

/// <summary>
/// Instructs the PolyType source generator to generate shapes for types annotated
/// with a registered attribute.
/// </summary>
/// <param name="attributeType">The attribute type that triggers shape generation.</param>
/// <remarks>
/// Annotated types must satisfy the same requirements as types annotated with
/// <see cref="GenerateShapeAttribute"/>, including being partial.
/// Unlike <see cref="GenerateShapeAttribute"/>, this annotation is retained in
/// metadata so registrations in referenced assemblies can trigger generation.
/// An explicit <see cref="GenerateShapeAttribute"/> on a type replaces these defaults.
/// When multiple registered attributes annotate a type, the first annotation supplies
/// the defaults. Registrations in the current assembly take precedence over references;
/// otherwise the first registration for an attribute type is used.
/// </remarks>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class GenerateShapeAttributeAttribute(Type attributeType) : Attribute
{
    /// <summary>
    /// Gets the attribute type that triggers shape generation.
    /// </summary>
    public Type AttributeType { get; } = attributeType;

    /// <inheritdoc cref="TypeShapeAttribute.Marshaler"/>
    public Type? Marshaler { get; init; }

    /// <inheritdoc cref="TypeShapeAttribute.Kind"/>
    public TypeShapeKind Kind { get; init; }

    /// <inheritdoc cref="TypeShapeAttribute.IncludeMethods"/>
    public MethodShapeFlags IncludeMethods { get; init; }

    /// <inheritdoc cref="TypeShapeAttribute.InferClosedTypePolymorphism"/>
    public bool InferClosedTypePolymorphism { get; init; }
}
