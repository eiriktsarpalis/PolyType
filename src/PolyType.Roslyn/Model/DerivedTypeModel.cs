using Microsoft.CodeAnalysis;

namespace PolyType.Roslyn;

/// <summary>
/// Represents a derived type defined in the context of a polymorphic type.
/// </summary>
public sealed class DerivedTypeModel
{
    /// <summary>
    /// The type symbol for the derived type.
    /// </summary>
    public required ITypeSymbol Type { get; init; }

    /// <summary>
    /// The string identifier of the derived type.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The numeric identifier of the derived type.
    /// </summary>
    public required int Tag { get; init; }

    /// <summary>
    /// The index of the derived type.
    /// </summary>
    public required int Index { get; init; }

    /// <summary>
    /// Whether the derived type equals the base type.
    /// </summary>
    public required bool IsBaseType { get; init; }
}
