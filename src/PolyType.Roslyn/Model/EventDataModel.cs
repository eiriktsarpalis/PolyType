using Microsoft.CodeAnalysis;

#nullable enable

namespace PolyType.Roslyn;

/// <summary>
/// An event data model wrapping an <see cref="IEventSymbol"/>.
/// </summary>
#if POLYTYPE_ROSLYN_PUBLIC
public
#else
internal
#endif
readonly struct EventDataModel
{
    /// <summary>
    /// The name used to identify the event in the generated code.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The event symbol that this model represents.
    /// </summary>
    public required IEventSymbol Event { get; init; }

    /// <summary>
    /// Gets a value indicating whether the member is ambiguous due to diamond inheritance.
    /// </summary>
    public bool IsAmbiguous { get; init; }
}

/// <summary>
/// An event data model wrapping an <see cref="IEventSymbol"/>.
/// </summary>
#if POLYTYPE_ROSLYN_PUBLIC
public
#else
internal
#endif
readonly struct ResolvedEventSymbol
{
    /// <summary>
    /// Gets the resolved event symbol.
    /// </summary>
    public required IEventSymbol Event { get; init; }

    /// <summary>
    /// Gets a custom name to be applied to the event, if specified.
    /// </summary>
    public string? CustomName { get; init; }

    /// <summary>
    /// Gets a value indicating whether the member is ambiguous due to diamond inheritance.
    /// </summary>
    public bool IsAmbiguous { get; init; }
}
