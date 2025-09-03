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
    /// The requirements for what needs to be generated for the type shape.
    /// </summary>
    public required TypeShapeRequirements Requirements { get; init; }

    /// <summary>
    /// Any methods that the type defines whose parameters should be included in the type graph traversal.
    /// </summary>
    public ImmutableArray<MethodDataModel> Methods { get; init; } = ImmutableArray<MethodDataModel>.Empty;

    /// <summary>
    /// Any events that the type defines whose event type should be included in the type graph traversal.
    /// </summary>
    public ImmutableArray<EventDataModel> Events { get; init; } = ImmutableArray<EventDataModel>.Empty;

    /// <summary>
    /// The list of known derived types for the given type in topological order from most to least derived.
    /// </summary>
    public ImmutableArray<DerivedTypeModel> DerivedTypes { get; init; } = ImmutableArray<DerivedTypeModel>.Empty;

    /// <summary>
    /// The collection of associated types specified via TypeShapeAttribute.AssociatedTypes.
    /// </summary>
    public ImmutableArray<AssociatedTypeModel> AssociatedTypes { get; set; } = ImmutableArray<AssociatedTypeModel>.Empty;

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
