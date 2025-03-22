using PolyType.Abstractions;

namespace PolyType.Examples.CborSerializer;

/// <summary>
/// Identifies a custom converter for the type.
/// </summary>
/// <param name="converterType"></param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
[AssociatedTypeAttribute(nameof(converterType))]
public class CborConverterAttribute(Type converterType) : Attribute
{
    /// <summary>
    /// Gets the type of the converter to use for the data type this attribute is applied to.
    /// </summary>
    public Type ConverterType => converterType;
}
