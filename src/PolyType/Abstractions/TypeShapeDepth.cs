namespace PolyType;

/// <summary>
/// Describes the requirements for preparing an associated type.
/// </summary>
/// <devremarks>
/// Keep this in sync with the TypeShapeDepth enum defined in the PolyType.SourceGenerator assembly.
/// </devremarks>
[Flags]
public enum TypeShapeDepth
{
    /// <summary>No shape is required.</summary>
    None = 0x0,

    /// <summary>
    /// A constructor should be included in the shape, if one is declared.
    /// </summary>
    Constructor = 0x1,

    /// <summary>
    /// Properties should be included in the shape, if any are declared.
    /// </summary>
    Properties = 0x2,

    /// <summary>
    /// The shape should be fully generated.
    /// </summary>
    All = Constructor | Properties,
}
