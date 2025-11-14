namespace PolyType.SourceGenModel;

/// <summary>
/// Describes a source generated attribute.
/// </summary>
public readonly struct SourceGenAttributeInfo
{
    /// <summary>
    /// Gets the value of the attribute.
    /// </summary>
    public required Attribute Attribute { get; init; }

    /// <summary>
    /// Gets whether the attribute is inherited from a base member.
    /// </summary>
    public bool IsInherited { get; init; }
}
