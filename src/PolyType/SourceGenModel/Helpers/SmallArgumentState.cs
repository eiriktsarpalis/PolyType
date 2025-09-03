using PolyType.Abstractions;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace PolyType.SourceGenModel;

/// <summary>
/// Defines an argument state for a constructor that can track a small (64 or fewer) number of arguments.
/// </summary>
/// <typeparam name="TArguments">The type storing the arguments.</typeparam>
[EditorBrowsable(EditorBrowsableState.Never)]
[StructLayout(LayoutKind.Auto)]
public struct SmallArgumentState<TArguments> : IArgumentState
{
    private readonly uint _count;
    private readonly ulong _requiredArgumentsMask;
    private ulong _setArguments;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmallArgumentState{TArguments}"/> struct.
    /// </summary>
    /// <param name="arguments">The initial argument state object.</param>
    /// <param name="count">The total number of arguments.</param>
    /// <param name="requiredArgumentsMask">A bitmask indicating which arguments are required.</param>
    public SmallArgumentState(TArguments arguments, int count, ulong requiredArgumentsMask)
    {
        if ((uint)count > 64)
        {
            Throw();
            static void Throw() => throw new ArgumentOutOfRangeException(nameof(count), "Count must be 64 or fewer.");
        }

        Arguments = arguments;
        _count = (uint)count;
        _requiredArgumentsMask = requiredArgumentsMask;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SmallArgumentState{TArguments}"/> struct.
    /// </summary>
    /// <param name="arguments">The initial argument state object.</param>
    /// <param name="count">The total number of arguments.</param>
    /// <param name="requiredArgumentsMask">A bitmask indicating which arguments are required.</param>
    /// <param name="markAllArgumentsSet">If <c>true</c>, all arguments are marked as set.</param>
    public SmallArgumentState(TArguments arguments, int count, ulong requiredArgumentsMask, bool markAllArgumentsSet)
        : this(arguments, count, requiredArgumentsMask)
    {
        if (markAllArgumentsSet)
        {
            _setArguments = (1UL << count) - 1;
        }
    }

    /// <summary>
    /// The actual arguments being tracked by this state.
    /// </summary>
#pragma warning disable CA1051 // Do not declare visible instance fields
    public TArguments Arguments;
#pragma warning restore CA1051 // Do not declare visible instance fields

    /// <inheritdoc />
    public readonly int Count => (int)_count;

    /// <inheritdoc />
    public readonly bool AreRequiredArgumentsSet => (_setArguments & _requiredArgumentsMask) == _requiredArgumentsMask;

    /// <inheritdoc />
    public readonly bool IsArgumentSet(int index)
    {
        if ((uint)index >= _count)
        {
            return false;
        }

        // Check if the bit at the specified index is set in _setArguments.
        return (_setArguments & (1UL << index)) != 0;
    }

    /// <summary>
    /// Marks the argument at the specified index as set.
    /// </summary>
    /// <param name="index">The index of the argument to mark as set.</param>
    public void MarkArgumentSet(int index)
    {
        if ((uint)index < _count)
        {
            _setArguments |= 1UL << index;
        }
    }
}
