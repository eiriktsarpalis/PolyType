namespace PolyType.Abstractions;

/// <summary>
/// Instructs the PolyType source generator to generate shapes for types annotated
/// with the attribute on which this annotation is placed.
/// </summary>
/// <remarks>
/// Annotated types must satisfy the same requirements as types annotated with
/// <see cref="GenerateShapeAttribute"/>, including being partial.
/// Unlike <see cref="GenerateShapeAttribute"/>, this annotation is retained in
/// metadata so attributes defined in referenced assemblies can trigger generation.
/// An explicit <see cref="GenerateShapeAttribute"/> on a type replaces these defaults.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class GenerateShapeAttributeAttribute : Attribute
{
    /// <inheritdoc cref="TypeShapeAttribute.Marshaler"/>
    public Type? Marshaler { get; init; }

    /// <inheritdoc cref="TypeShapeAttribute.Kind"/>
    public TypeShapeKind Kind { get; init; }

    /// <inheritdoc cref="TypeShapeAttribute.IncludeMethods"/>
    public MethodShapeFlags IncludeMethods { get; init; }

    /// <inheritdoc cref="TypeShapeAttribute.InferClosedTypePolymorphism"/>
    public bool InferClosedTypePolymorphism { get; init; }
}
