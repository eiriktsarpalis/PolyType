using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace PolyType.Roslyn;

/// <summary>
/// Exposes a data model extracted from an <see cref="ITypeSymbol"/> input.
/// </summary>
public class TypeDataModel
{
    /// <summary>
    /// The <see cref="ITypeSymbol"/> that this model represents.
    /// </summary>
    public required ITypeSymbol Type { get; init; }

    /// <summary>
    /// The depth that was applied when this shape was generated.
    /// </summary>
    public required TypeShapeDepth Depth { get; init; }

    /// <summary>
    /// The list of known derived types for the given type in topological order from most to least derived.
    /// </summary>
    public ImmutableArray<DerivedTypeModel> DerivedTypes { get; init; } = ImmutableArray<DerivedTypeModel>.Empty;

    /// <summary>
    /// The collection of associated types specified via TypeShapeAttribute.AssociatedTypes.
    /// </summary>
    public ImmutableArray<AssociatedTypeModel> AssociatedTypes { get; init; } = ImmutableArray<AssociatedTypeModel>.Empty;

    /// <summary>
    /// Determines the type of <see cref="TypeDataModel"/> being used.
    /// </summary>
    public virtual TypeDataKind Kind => TypeDataKind.None;

    /// <summary>
    /// True if the type was explicitly passed to the <see cref="TypeDataModelGenerator"/>
    /// and is not a transitive dependency in the type graph.
    /// </summary>
    public bool IsRootType { get; internal set; }
}
