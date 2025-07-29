using PolyType.Abstractions;
using System.ComponentModel;

namespace PolyType.SourceGenModel;

/// <summary>
/// Denotes an argument state corresponding to a type that accepts no constructor arguments.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public abstract class EmptyArgumentState : IArgumentState
{
    // There is no requirement to construct an instance of this type,
    // it is just needed to satisfy the IArgumentState generic constraints.
    private EmptyArgumentState() { }

    /// <inheritdoc/>
    public int Count => 0;

    /// <inheritdoc/>
    public bool AreRequiredArgumentsSet => true;

    /// <inheritdoc/>
    public bool IsArgumentSet(int index) => false;
}
