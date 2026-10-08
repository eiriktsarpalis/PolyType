using PolyType.Abstractions;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PolyType.SourceGenModel;

/// <summary>
/// Defines a pooled reference-type argument state for up to 64 arguments.
/// </summary>
/// <typeparam name="TArguments">The type storing the arguments.</typeparam>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class SmallClassArgumentState<TArguments> : IArgumentState
{
    [ThreadStatic]
    private static SmallClassArgumentState<TArguments>? t_cached;
    private uint _count;
    private ulong _requiredArgumentsMask;
    private ulong _setArguments;

    private SmallClassArgumentState() { }

    /// <summary>
    /// Rents an argument state initialized with the specified arguments and required-argument mask.
    /// </summary>
    /// <param name="arguments">The initial arguments.</param>
    /// <param name="count">The total number of arguments.</param>
    /// <param name="requiredArgumentsMask">A mask identifying required arguments.</param>
    /// <param name="markAllArgumentsSet">Whether all arguments should initially be marked as set.</param>
    /// <returns>A reusable argument state instance.</returns>
    public static SmallClassArgumentState<TArguments> Rent(in TArguments arguments, int count, ulong requiredArgumentsMask, bool markAllArgumentsSet = false)
    {
        if ((uint)count > 64)
        {
            Throw();
            static void Throw() => throw new ArgumentOutOfRangeException(nameof(count), "Count must be 64 or fewer.");
        }

        SmallClassArgumentState<TArguments>? state = t_cached;
        t_cached = null;
        state ??= new();
        state.Arguments = arguments;
        state._count = (uint)count;
        state._requiredArgumentsMask = requiredArgumentsMask;
        state._setArguments = markAllArgumentsSet
            ? count == 64 ? ulong.MaxValue : (1UL << count) - 1
            : 0;
        return state;
    }

    /// <summary>
    /// The actual arguments being tracked by this state.
    /// </summary>
#pragma warning disable CA1051 // Do not declare visible instance fields: generated accessors require direct field access.
#pragma warning disable SA1401
    public TArguments Arguments = default!;
#pragma warning restore CA1051, SA1401

    /// <inheritdoc />
    public int Count => (int)_count;

    /// <inheritdoc />
    public bool AreRequiredArgumentsSet => (_setArguments & _requiredArgumentsMask) == _requiredArgumentsMask;

    /// <inheritdoc />
    public bool IsArgumentSet(int index) =>
        (uint)index < _count && (_setArguments & (1UL << index)) != 0;

    /// <summary>
    /// Marks the argument at the specified index as set.
    /// </summary>
    /// <param name="index">The index of the argument to mark.</param>
    public void MarkArgumentSet(int index)
    {
        if ((uint)index < _count)
        {
            _setArguments |= 1UL << index;
        }
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        // Rent overwrites the payload and tracking fields; only references need clearing here.
        t_cached = this;
    }
}
