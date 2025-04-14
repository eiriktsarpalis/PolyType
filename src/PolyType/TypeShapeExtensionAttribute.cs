using PolyType.Abstractions;
using System.Diagnostics;

namespace PolyType;

/// <summary>
/// An assembly-level attribute that can extend an existing type's generated shape,
/// as if <see cref="TypeShapeAttribute"/> had been applied to the target type.
/// </summary>
/// <param name="target">The type to be extended.</param>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public class TypeShapeExtensionAttribute(Type target) : Attribute
{
    /// <summary>
    /// Gets the target type.
    /// </summary>
    /// <remarks>
    /// This is the type that is being extended.
    /// If the type is declared in the same assembly that contains this attribute,
    /// consider removing this attribute in favor of applying <see cref="TypeShapeAttribute"/>
    /// directly to the target type.
    /// </remarks>
    public Type Target => target;

    /// <summary>
    /// Gets the elements of the generated shape that are required at runtime.
    /// </summary>
    /// <value>The default value is <see cref="TypeShapeDepth.All"/>.</value>
    public TypeShapeDepth AssociatedShapeDepth { get; init; } = TypeShapeDepth.All;

    /// <summary>
    /// Types for which a shape should be generated when a type shape is generated for <see cref="Target"/>.
    /// </summary>
    /// <remarks>
    /// If <see cref="Target"/> is a generic type definition, each type in this array must also be a generic type definition
    /// with the same number of generic type parameters.
    /// </remarks>
    public Type[] AssociatedTypes { get; init; } = [];
}
