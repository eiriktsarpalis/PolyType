using PolyType.Roslyn;

namespace PolyType.SourceGenerator.Model;

/// <summary>
/// Represents an attribute to be emitted in source-generated code.
/// </summary>
public sealed record AttributeDataModel
{
    /// <summary>
    /// The fully qualified type name of the attribute.
    /// </summary>
    public required TypeId AttributeType { get; init; }

    /// <summary>
    /// Whether the attribute is inherited from a base symbol.
    /// </summary>
    public required bool IsInherited { get; init; }

    /// <summary>
    /// The constructor arguments for the attribute.
    /// </summary>
    public required ImmutableEquatableArray<string> ConstructorArguments { get; init; }

    /// <summary>
    /// The named arguments for the attribute (property or field initializers).
    /// </summary>
    public required ImmutableEquatableArray<(string Name, string Value)> NamedArguments { get; init; }
}
