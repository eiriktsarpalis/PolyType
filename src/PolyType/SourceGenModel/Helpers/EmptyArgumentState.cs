using PolyType.Abstractions;
using System.ComponentModel;

namespace PolyType.SourceGenModel;

/// <summary>
/// Denotes an argument state corresponding to a type that accepts no constructor arguments.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class EmptyArgumentState : IArgumentState
{
    /// <summary>
    /// Gets the default empty argument state instance.
    /// </summary>
    public static EmptyArgumentState Instance { get; } = new();

    private EmptyArgumentState() { }

    /// <inheritdoc/>
    public int Count => 0;

    /// <inheritdoc/>
    public bool AreRequiredArgumentsSet => true;

    /// <inheritdoc/>
    public bool IsArgumentSet(int index) => false;
}
