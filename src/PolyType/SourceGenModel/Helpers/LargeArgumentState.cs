using PolyType.Abstractions;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace PolyType.SourceGenModel;

/// <summary>
/// Defines an argument state for a constructor that can track a large (more than 64) number of arguments.
/// </summary>
/// <typeparam name="TArguments">The type storing the arguments.</typeparam>
[EditorBrowsable(EditorBrowsableState.Never)]
[StructLayout(LayoutKind.Auto)]
public struct LargeArgumentState<TArguments> : IArgumentState
{
    private readonly ValueBitArray _requiredArgumentsMask;
    private readonly ValueBitArray _setArguments;

    /// <summary>
    /// Initializes a new instance of the <see cref="LargeArgumentState{TArguments}"/> struct.
    /// </summary>
    /// <param name="arguments">The initial argument state object.</param>
    /// <param name="count">The total number of arguments.</param>
    /// <param name="requiredArgumentsMask">A bitmask indicating which arguments are required.</param>
    public LargeArgumentState(TArguments arguments, int count, ValueBitArray requiredArgumentsMask)
    {
        if (requiredArgumentsMask.Length != count)
        {
            Throw();
            static void Throw() => throw new ArgumentOutOfRangeException(nameof(requiredArgumentsMask), "The length of the required arguments mask must match the count.");
        }

        Arguments = arguments;
        _requiredArgumentsMask = requiredArgumentsMask;
        _setArguments = new ValueBitArray(count);
    }

    /// <summary>
    /// The actual arguments being tracked by this state.
    /// </summary>
#pragma warning disable CA1051 // Do not declare visible instance fields
    public TArguments Arguments;
#pragma warning restore CA1051 // Do not declare visible instance fields

    /// <inheritdoc />
    public readonly int Count => _setArguments.Length;

    /// <inheritdoc />
    public readonly bool AreRequiredArgumentsSet => _requiredArgumentsMask.IsSubsetOf(_setArguments);

    /// <inheritdoc />
    public readonly bool IsArgumentSet(int index) => _setArguments[index];

    /// <summary>
    /// Marks the argument at the specified index as set.
    /// </summary>
    /// <param name="index">The index of the argument to mark as set.</param>
    public readonly void MarkArgumentSet(int index) => _setArguments[index] = true;
}
