using System.Collections.Immutable;

#nullable enable

namespace PolyType.Roslyn;

/// <summary>
/// Represents a type whose data model is defined as a list of properties.
/// </summary>
#if POLYTYPE_ROSLYN_PUBLIC
public
#else
internal
#endif
sealed class ObjectDataModel : TypeDataModel
{
    /// <inheritdoc/>
    public override TypeDataKind Kind => TypeDataKind.Object;

    /// <summary>
    /// List of instance properties or fields defined on the type.
    /// </summary>
    public required ImmutableArray<PropertyDataModel> Properties { get; init; }

    /// <summary>
    /// List of instance constructors defined on the type.
    /// </summary>
    public required ImmutableArray<ConstructorDataModel> Constructors { get; init; }
}
