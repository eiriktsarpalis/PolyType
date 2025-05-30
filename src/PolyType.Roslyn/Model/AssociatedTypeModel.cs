﻿using Microsoft.CodeAnalysis;

namespace PolyType.Roslyn;

/// <summary>
/// Represents a type associated with some type shape.
/// </summary>
/// <param name="AssociatedType">The symbol for the related type.</param>
/// <param name="AssociatingAssembly">The assembly that declared the association.</param>
/// <param name="Location">The location where the association is declared.</param>
/// <param name="Requirements">The associated type's requirements.</param>
public record struct AssociatedTypeModel(INamedTypeSymbol AssociatedType, IAssemblySymbol AssociatingAssembly, Location? Location, TypeShapeRequirements Requirements);

/// <summary>
/// Describes the requirements for preparing an associated type.
/// </summary>
/// <devremarks>
/// Keep this in sync with the TypeShapeRequirements enum defined in the PolyType assembly.
/// </devremarks>
[Flags]
public enum TypeShapeRequirements
{
    /// <summary>No shape is required.</summary>
    None = 0x0,

    /// <summary>
    /// A constructor, its parameters and their types should be included in the shape, if one is declared.
    /// </summary>
    Constructor = 0x1,

    /// <summary>
    /// Properties and their types should be included in the shape, if any are declared.
    /// </summary>
    Properties = 0x2,

    /// <summary>
    /// The shape should be fully generated.
    /// </summary>
    Full = Constructor | Properties,
}
