using Microsoft.CodeAnalysis;

namespace PolyType.Roslyn;

/// <summary>
/// Represents an <see cref="Enum"/> type data model.
/// </summary>
public sealed class EnumDataModel : TypeDataModel
{
    /// <inheritdoc/>
    public override TypeDataKind Kind => TypeDataKind.Enum;

    /// <summary>
    /// The underlying numeric type used by the enum.
    /// </summary>
    public required ITypeSymbol UnderlyingType { get; init; }

    /// <summary>
    /// The members of the enum, represented as a dictionary of member names to their underlying values.
    /// </summary>
    public required IReadOnlyDictionary<string, object> Members { get; init; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets a value indicating whether the enum is annotated with the <see cref="FlagsAttribute"/>.
    /// </summary>
    public bool IsFlags { get; init; }
}
