using Microsoft.CodeAnalysis;
using PolyType.Roslyn;
using System.Collections.Immutable;

namespace PolyType.SourceGenerator.Model;

/// <summary>
/// Captures the data collected from all the TypeShapeExtensionAttribute attributes that target a particular type.
/// </summary>
internal sealed record TypeExtensionModel
{
    /// <summary>
    /// The kind of type shape that should be generated for the <see cref="Target"/> type.
    /// </summary>
    public required TypeShapeKind? Kind { get; init; }

    /// <summary>
    /// Gets an optional marshaler type that can be used to convert the <see cref="Target"/> type to a surrogate type.
    /// </summary>
    public required INamedTypeSymbol? Marshaler { get; init; }

    /// <summary>
    /// Gets the binding flags that determine what method shapes should be included in the type shape.
    /// </summary>
    public required MethodShapeFlags? IncludeMethods { get; init; }

    /// <summary>
    /// An aggregate of all the associated types registered with the <see cref="Target"/>.
    /// </summary>
    public required ImmutableArray<AssociatedTypeModel> AssociatedTypes { get; init; } = ImmutableArray<AssociatedTypeModel>.Empty;

    /// <summary>
    /// Gets the locations of the <see cref="TypeShapeExtensionAttribute"/> attributes that target the <see cref="Target"/> type.
    /// </summary>
    public required ImmutableArray<Location> Locations { get; init; } = ImmutableArray<Location>.Empty;

    /// <summary>
    /// Creates a new <see cref="TypeExtensionModel"/> that combines two instances.
    /// </summary>
    public static TypeExtensionModel? Combine(TypeExtensionModel? primary, TypeExtensionModel? secondary)
    {
        if (primary is null || secondary is null)
        {
            return primary ?? secondary;
        }

        return new TypeExtensionModel
        {
            Kind = primary.Kind ?? secondary.Kind,
            Marshaler = primary.Marshaler ?? secondary.Marshaler,
            IncludeMethods = primary.IncludeMethods ?? secondary.IncludeMethods,
            AssociatedTypes = primary.AssociatedTypes.AddRange(secondary.AssociatedTypes),
            Locations = primary.Locations.AddRange(secondary.Locations)
        };
    }
}
