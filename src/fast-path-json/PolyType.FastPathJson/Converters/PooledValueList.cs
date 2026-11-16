using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PolyType.FastPathJson.Converters;

internal struct PooledValueList<T> : IDisposable
{
    private T[] _buffer;
    private int _count;

    public PooledValueList() => _buffer = ArrayPool<T>.Shared.Rent(4);

    public readonly int Count => _count;

    public void Add(T value)
    {
        if (_count == _buffer.Length)
        {
            T[] newBuffer = ArrayPool<T>.Shared.Rent(_buffer.Length * 2);
            _buffer.AsSpan(0, _count).CopyTo(newBuffer);
            ReturnBuffer(_buffer, _count);
            _buffer = newBuffer;
        }

        _buffer[_count++] = value;
    }

    public readonly T[] ToArray() => _buffer.AsSpan(0, _count).ToArray();

    public readonly List<T> ToList()
    {
        List<T> result = [];
        CollectionsMarshal.SetCount(result, _count);
        _buffer.AsSpan(0, _count).CopyTo(CollectionsMarshal.AsSpan(result));
        return result;
    }

    public void Dispose()
    {
        ReturnBuffer(_buffer, _count);
        _buffer = [];
        _count = 0;
    }

    private static void ReturnBuffer(T[] buffer, int count)
    {
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            buffer.AsSpan(0, count).Clear();
        }

        ArrayPool<T>.Shared.Return(buffer);
    }
}
