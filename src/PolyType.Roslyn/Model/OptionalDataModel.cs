using Microsoft.CodeAnalysis;
using System;

#nullable enable

namespace PolyType.Roslyn;

/// <summary>
/// Represents an optional type such as <see cref="Nullable{T}"/>.
/// </summary>
#if POLYTYPE_ROSLYN_PUBLIC
public
#else
internal
#endif
sealed class OptionalDataModel : TypeDataModel
{
    /// <inheritdoc/>
    public override TypeDataKind Kind => TypeDataKind.Optional;

    /// <summary>
    /// Gets the underlying element type of the optional type.
    /// </summary>
    public required ITypeSymbol ElementType { get; init; }
}
