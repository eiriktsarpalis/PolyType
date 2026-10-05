using PolyType.Abstractions;
using System.ComponentModel;

namespace PolyType.SourceGenModel;

/// <summary>
/// Defines a pooled reference-type argument state for more than 64 arguments.
/// </summary>
/// <typeparam name="TArguments">The type storing the arguments.</typeparam>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class ClassLargeArgumentState<TArguments> : IArgumentState
{
    [ThreadStatic]
    private static ClassLargeArgumentState<TArguments>? t_cached;
    private ValueBitArray _requiredArgumentsMask;
    private ValueBitArray _setArguments;

    private ClassLargeArgumentState() { }

    /// <summary>
    /// Rents an argument state initialized with the specified arguments and required-argument mask.
    /// </summary>
    /// <param name="arguments">The initial arguments.</param>
    /// <param name="count">The total number of arguments.</param>
    /// <param name="requiredArgumentsMask">A mask identifying required arguments.</param>
    /// <param name="markAllArgumentsSet">Whether all arguments should initially be marked as set.</param>
    /// <returns>A reusable argument state instance.</returns>
    public static ClassLargeArgumentState<TArguments> Rent(TArguments arguments, int count, ValueBitArray requiredArgumentsMask, bool markAllArgumentsSet = false)
    {
        if (requiredArgumentsMask.Length != count)
        {
            Throw();
            static void Throw() => throw new ArgumentOutOfRangeException(nameof(requiredArgumentsMask), "The length of the required arguments mask must match the count.");
        }

        ClassLargeArgumentState<TArguments>? state = t_cached;
        t_cached = null;
        state ??= new();
        state.Arguments = arguments;
        state._requiredArgumentsMask = requiredArgumentsMask;
        if (state._setArguments.Length != count)
        {
            state._setArguments = new ValueBitArray(count);
        }
        else
        {
            state._setArguments.SetAll(false);
        }

        if (markAllArgumentsSet)
        {
            state._setArguments.SetAll(true);
        }

        return state;
    }

    /// <summary>
    /// The actual arguments being tracked by this state.
    /// </summary>
#pragma warning disable CA1051, SA1401
    public TArguments Arguments = default!;
#pragma warning restore CA1051, SA1401

    /// <inheritdoc />
    public int Count => _setArguments.Length;

    /// <inheritdoc />
    public bool AreRequiredArgumentsSet => _requiredArgumentsMask.IsSubsetOf(_setArguments);

    /// <inheritdoc />
    public bool IsArgumentSet(int index) => _setArguments[index];

    /// <inheritdoc />
    public bool IsPoolable => true;

    /// <summary>
    /// Marks the argument at the specified index as set.
    /// </summary>
    /// <param name="index">The index of the argument to mark.</param>
    public void MarkArgumentSet(int index) => _setArguments[index] = true;

    /// <inheritdoc />
    public void Return()
    {
        Arguments = default!;
        _requiredArgumentsMask = default;
        t_cached = this;
    }
}
