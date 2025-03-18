using Microsoft.CodeAnalysis;

namespace PolyType.Roslyn;

/// <summary>
/// Represents a type associated with some type shape.
/// </summary>
/// <param name="AssociatedType">The symbol for the related type.</param>
/// <param name="AssociatingAssembly">The assembly that declared the association.</param>
/// <param name="Location">The location where the association is declared.</param>
public record struct AssociatedTypeModel(INamedTypeSymbol AssociatedType, IAssemblySymbol AssociatingAssembly, Location? Location);
