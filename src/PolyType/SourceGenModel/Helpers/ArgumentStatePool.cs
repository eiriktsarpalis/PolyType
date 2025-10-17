using System.Runtime.CompilerServices;
using System.Collections.Concurrent;
using System.Threading;

namespace PolyType.SourceGenModel;

/// <summary>
/// Defines a thread-safe pool for reusing <see cref="System.Runtime.CompilerServices.StrongBox{T}"/> instances.
/// </summary>
public sealed class StrongBoxPool<T> where T : struct
{
    // Bounded pool size to avoid unbounded retention of boxed struct values.
    private const int MaxPoolSize = 256;
    private readonly ConcurrentQueue<StrongBox<T>> _queue = new();
    private int _count; // number of items currently stored in the queue

    /// <summary>Rents a <see cref="StrongBox{T}"/> whose <see cref="StrongBox{T}.Value"/> is initialized to <paramref name="value"/>.</summary>
    public StrongBox<T> Rent(T value)
    {
        if (_queue.TryDequeue(out StrongBox<T>? box))
        {
            Interlocked.Decrement(ref _count);
            box.Value = value; // overwrite previous value
            return box;
        }

        return new(value);
    }

    /// <summary>Returns a previously rented <see cref="StrongBox{T}"/> to the pool.</summary>
    public void Return(StrongBox<T> box)
    {
        if (box is null)
        {
            return;
        }

        // Clear value to avoid holding onto large structs inadvertently.
        box.Value = default;

        // Fast path: if we've already reached capacity just drop the box.
        if (Volatile.Read(ref _count) >= MaxPoolSize)
        {
            return; // let it be GC'ed
        }

        _queue.Enqueue(box);
        // If we went over capacity due to a race we simply allow a slight overflow.
        Interlocked.Increment(ref _count);
    }
}
