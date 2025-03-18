using PolyType.Abstractions;
using System.Diagnostics;

namespace PolyType;

/// <summary>
/// An assembly-level attribute that can extend an existing type's generated shape,
/// as if <see cref="TypeShapeAttribute"/> had been applied to the target type.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public class TypeShapeExtensionAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TypeShapeExtensionAttribute"/> class.
    /// </summary>
    /// <param name="target">The type to be extended.</param>
    public TypeShapeExtensionAttribute(Type target)
    {
        Target = target;
    }

    /// <summary>
    /// Gets the target type.
    /// </summary>
    /// <remarks>
    /// This is the type that is being extended.
    /// If the type is declared in the same assembly that contains this attribute,
    /// consider removing this attribute in favor of applying <see cref="TypeShapeAttribute"/>
    /// directly to the target type.
    /// </remarks>
    public Type Target { get; }

    /// <summary>
    /// Types for which a factory should be generated when a type shape is generated for <see cref="Target"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each type must declare a public default constructor.
    /// </para>
    /// <para>
    /// If <see cref="Target"/> is a generic type definition, each type in this array must also be a generic type definition
    /// with the same number of generic type parameters.
    /// </para>
    /// </remarks>
    public Type[] AssociatedTypes { get; init; } = [];
}
