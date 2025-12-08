#pragma warning disable CA1512 // Use ArgumentOutOfRangeException throw helper

using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PolyType.SourceGenModel;

/// <summary>
/// Helper class for iterating over inline arrays.
/// </summary>
/// <typeparam name="TArray">The type of the inline array.</typeparam>
/// <typeparam name="TElement">The type of the element.</typeparam>
public sealed class InlineArrayEnumerable<TArray, TElement>(TArray array, int length) : ICollection<TElement>, IReadOnlyCollection<TElement>
    where TArray : struct
{
    /// <inheritdoc/>
    public int Count => length;

    /// <inheritdoc/>
    public bool IsReadOnly => true;

    /// <inheritdoc/>
    public void Add(TElement item) => throw new NotSupportedException();

    /// <inheritdoc/>
    public void Clear() => throw new NotSupportedException();

    /// <inheritdoc/>
    public bool Contains(TElement item)
    {
        foreach (var element in this)
        {
            if (EqualityComparer<TElement>.Default.Equals(element, item))
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc/>
    public void CopyTo(TElement[] array, int arrayIndex)
    {
        if (array is null)
        {
            throw new ArgumentNullException(nameof(array));
        }

        if (arrayIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(arrayIndex));
        }

        if (array.Length - arrayIndex < length)
        {
            throw new ArgumentException("Destination array is too small.");
        }

        int i = 0;
        foreach (var element in this)
        {
            array[arrayIndex + i++] = element;
        }
    }

    /// <inheritdoc/>
    public bool Remove(TElement item) => throw new NotSupportedException();

    /// <inheritdoc/>
    public IEnumerator<TElement> GetEnumerator() => new Enumerator(array, length);
    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

    /// <summary>
    /// Enumerator for the inline array.
    /// </summary>
    public struct Enumerator : IEnumerator<TElement>
    {
        private readonly int _length;
        private TArray _array;
        private int _index;

        /// <summary>
        /// Initializes a new instance of the <see cref="Enumerator"/> struct.
        /// </summary>
        /// <param name="array">The inline array to enumerate.</param>
        /// <param name="length">The number of elements in the array.</param>
        public Enumerator(TArray array, int length)
        {
            _array = array;
            _length = length;
            _index = -1;
        }

        /// <inheritdoc/>
        public TElement Current
        {
            get
            {
                if (_index >= _length || _index < 0)
                {
                    throw new InvalidOperationException();
                }

                return Unsafe.Add(ref Unsafe.As<TArray, TElement>(ref _array), _index);
            }
        }

        object? IEnumerator.Current => Current;

        /// <inheritdoc/>
        public bool MoveNext() => ++_index < _length;

        /// <inheritdoc/>
        public void Reset() => _index = -1;

        /// <inheritdoc/>
        public void Dispose() { }
    }
}
