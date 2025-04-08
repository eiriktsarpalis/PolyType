using Microsoft.CodeAnalysis;

namespace PolyType.Roslyn;

/// <summary>
/// Represents a type associated with some type shape.
/// </summary>
/// <param name="AssociatedType">The symbol for the related type.</param>
/// <param name="AssociatingAssembly">The assembly that declared the association.</param>
/// <param name="Location">The location where the association is declared.</param>
/// <param name="Requirements">The associated type's requirements.</param>
public record struct AssociatedTypeModel(INamedTypeSymbol AssociatedType, IAssemblySymbol AssociatingAssembly, Location? Location, AssociatedTypeRequirements Requirements);

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
