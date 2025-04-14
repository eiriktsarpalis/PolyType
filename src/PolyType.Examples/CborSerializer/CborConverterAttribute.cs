using PolyType.Abstractions;

namespace PolyType.Examples.CborSerializer;

/// <summary>
/// Identifies a custom converter for the type.
/// </summary>
/// <param name="converterType"></param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
[AssociatedTypeAttribute(nameof(converterType), TypeShapeDepth.Constructor)]
[AssociatedTypeAttribute(nameof(RequiredShapes), TypeShapeDepth.All)]
public class CborConverterAttribute(Type converterType) : Attribute
{
    /// <summary>
    /// Gets the type of the converter to use for the data type this attribute is applied to.
    /// </summary>
    public Type ConverterType => converterType;

    /// <summary>
    /// Gets or sets the types for which shapes should be generated when the type this attribute is applied on
    /// is used as a shape.
    /// </summary>
    /// <remarks>
    /// If these are unbound generic types, they will be bound with the same generic type arguments used
    /// to generate the shape of the type this attribute is applied to.
    /// </remarks>
    public Type[] RequiredShapes { get; set; } = [];
}
