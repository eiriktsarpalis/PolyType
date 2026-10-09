using PolyType.Abstractions;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace PolyType.SourceGenModel;

/// <summary>
/// Defines a pooled reference-type argument state for more than 64 arguments.
/// </summary>
/// <typeparam name="TArguments">The type storing the arguments.</typeparam>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class LargeClassArgumentState<TArguments> : IArgumentState
{
    [ThreadStatic]
    private static LargeClassArgumentState<TArguments>? t_cached;
    private ValueBitArray _requiredArgumentsMask;
    private ValueBitArray _setArguments;

    private LargeClassArgumentState(in TArguments arguments, int count, ValueBitArray requiredArgumentsMask, bool markAllArgumentsSet)
    {
        Debug.Assert(requiredArgumentsMask.Length == count);
        Arguments = arguments;
        Initialize(count, requiredArgumentsMask, markAllArgumentsSet);
    }

    /// <summary>
    /// Rents an argument state initialized with the specified arguments and required-argument mask.
    /// </summary>
    /// <param name="arguments">A read-only reference to the initial arguments.</param>
    /// <param name="count">The total number of arguments.</param>
    /// <param name="requiredArgumentsMask">A mask identifying required arguments.</param>
    /// <param name="markAllArgumentsSet">Whether all arguments should initially be marked as set.</param>
    /// <returns>A reusable argument state instance.</returns>
    public static LargeClassArgumentState<TArguments> Rent(in TArguments arguments, int count, ValueBitArray requiredArgumentsMask, bool markAllArgumentsSet = false)
    {
        if (requiredArgumentsMask.Length != count)
        {
            Throw();
            static void Throw() => throw new ArgumentOutOfRangeException(nameof(requiredArgumentsMask), "The length of the required arguments mask must match the count.");
        }

        LargeClassArgumentState<TArguments>? state = t_cached;
        if (state is null)
        {
            state = new(in arguments, count, requiredArgumentsMask, markAllArgumentsSet);
        }
        else
        {
            state.Arguments = arguments;
            state.Initialize(count, requiredArgumentsMask, markAllArgumentsSet);
            t_cached = null;
        }

        return state;
    }

    /// <summary>
    /// The actual arguments being tracked by this state.
    /// </summary>
#pragma warning disable CA1051, SA1401 // Do not declare visible instance fields: generated accessors require direct field access.
    public TArguments Arguments;
#pragma warning restore CA1051, SA1401

    /// <inheritdoc />
    public int Count => _setArguments.Length;

    /// <inheritdoc />
    public bool AreRequiredArgumentsSet => _requiredArgumentsMask.IsSubsetOf(_setArguments);

    /// <inheritdoc />
    public bool IsArgumentSet(int index) => _setArguments[index];

    /// <summary>
    /// Marks the argument at the specified index as set.
    /// </summary>
    /// <param name="index">The index of the argument to mark.</param>
    public void MarkArgumentSet(int index) => _setArguments[index] = true;

    /// <inheritdoc />
    public void Return()
    {
#if NET
        if (RuntimeHelpers.IsReferenceOrContainsReferences<TArguments>())
        {
            Arguments = default!;
        }
#else
        Arguments = default!;
#endif
        _requiredArgumentsMask = default;
        t_cached = this;
    }

    private void Initialize(int count, ValueBitArray requiredArgumentsMask, bool markAllArgumentsSet)
    {
        _requiredArgumentsMask = requiredArgumentsMask;
        if (_setArguments.Length != count)
        {
            _setArguments = new ValueBitArray(count);
            if (markAllArgumentsSet)
            {
                _setArguments.SetAll(true);
            }
        }
        else
        {
            _setArguments.SetAll(markAllArgumentsSet);
        }
    }
}
