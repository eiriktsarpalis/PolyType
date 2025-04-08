namespace PolyType.Abstractions;

/// <summary>
/// Describes the requirements for preparing an associated type.
/// </summary>
[Flags]
public enum AssociatedTypeRequirements
{
    /// <summary>No requirements.</summary>
    None = 0x0,

    /// <summary>
    /// A factory method should be prepared for the associated type.
    /// </summary>
    Factory = 0x1,

    /// <summary>
    /// An ITypeShape should be generated for the associated type.
    /// </summary>
    Shape = 0x2,
}
