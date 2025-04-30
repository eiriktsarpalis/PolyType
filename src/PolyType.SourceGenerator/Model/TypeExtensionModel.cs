using Microsoft.CodeAnalysis;
using PolyType.Roslyn;
using System.Collections.Immutable;

namespace PolyType.SourceGenerator.Model;

/// <summary>
/// Captures the data collected from all the TypeShapeExtensionAttribute attributes that target a particular type.
/// </summary>
public sealed record TypeExtensionModel
{
    /// <summary>
    /// The target type of the extension.
    /// </summary>
    public required INamedTypeSymbol Target { get; init; }

    /// <summary>
    /// An aggregate of all the associated types registered with the <see cref="Target"/>.
    /// </summary>
    public ImmutableArray<AssociatedTypeModel> AssociatedTypes { get; init; } = ImmutableArray<AssociatedTypeModel>.Empty;

    /// <summary>
    /// Gets an optional marshaller type that can be used to convert the <see cref="Target"/> type to a surrogate type.
    /// </summary>
    public INamedTypeSymbol? Marshaller { get; init; }

    /// <summary>
    /// Gets the locations of the <see cref="TypeShapeExtensionAttribute"/> attributes that target the <see cref="Target"/> type.
    /// </summary>
    public ImmutableArray<Location> Locations { get; init; } = ImmutableArray<Location>.Empty;
}
