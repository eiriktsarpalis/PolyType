using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace PolyType.Roslyn;

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
    public required ImmutableArray<INamedTypeSymbol> AssociatedTypes { get; init; }
}
