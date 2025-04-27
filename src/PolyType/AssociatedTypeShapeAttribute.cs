using PolyType.Abstractions;

namespace PolyType;

/// <summary>
/// Identifies one or more associated types for which shapes should be generated
/// when the type this attribute is applied to has a shape generated for it.
/// </summary>
/// <param name="associatedTypes">The types whose shapes should be generated.</param>
/// <remarks>
/// If the type this attribute is applied to is a generic type definition,
/// each type in this array must also be a generic type definition
/// with the same number of generic type parameters.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Enum, AllowMultiple = true, Inherited = false)]
public class AssociatedTypeShapeAttribute(params Type[] associatedTypes) : Attribute
{
    /// <summary>
    /// Gets the elements of the generated shape that are required at runtime.
    /// </summary>
    /// <value>The default value is <see cref="TypeShapeRequirements.Full"/>.</value>
    /// <remarks>
    /// This property only impacts generation of object shapes.
    /// All other shapes (e.g. collections, enums, unions) are always completely defined.
    /// </remarks>
    public TypeShapeRequirements Requirements { get; init; } = TypeShapeRequirements.Full;

    /// <summary>
    /// Gets the types whose shapes should be generated.
    /// </summary>
    public Type[] AssociatedTypes => associatedTypes;
}
