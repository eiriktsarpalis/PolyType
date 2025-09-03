using Microsoft.CodeAnalysis;

namespace PolyType.Roslyn;

/// <summary>
/// A method data model wrapping an <see cref="IEventSymbol"/>.
/// </summary>
public readonly struct EventDataModel
{
    /// <summary>
    /// The name used to identify the event in the generated code.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The event symbol that this model represents.
    /// </summary>
    public required IEventSymbol Event { get; init; }
}

/// <summary>
/// A method data model wrapping an <see cref="IEventSymbol"/>.
/// </summary>
public readonly struct ResolvedEventSymbol
{
    /// <summary>
    /// Gets the resolved event symbol.
    /// </summary>
    public required IEventSymbol Event { get; init; }

    /// <summary>
    /// Gets a custom name to be applied to the method, if specified.
    /// </summary>
    public string? CustomName { get; init; }
}
