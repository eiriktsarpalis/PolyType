using System;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace PolyType.Roslyn;

/// <summary>
/// Defines an immutable array that defines structural equality semantics.
/// </summary>
/// <remarks>
/// The default value represents an empty array.
/// The collection is immutable and reports <see cref="ICollection.IsSynchronized"/> as true.
/// Accessing <see cref="ICollection.SyncRoot"/> throws <see cref="NotSupportedException"/>.
/// </remarks>
/// <typeparam name="T">The type of the array elements.</typeparam>
[DebuggerDisplay("Length = {Length}")]
[DebuggerTypeProxy(typeof(ImmutableEquatableArray<>.DebugView))]
[CollectionBuilder(typeof(ImmutableEquatableArray), nameof(ImmutableEquatableArray.Create))]
public readonly struct ImmutableEquatableArray<T> :
    IEquatable<ImmutableEquatableArray<T>>, 
    IReadOnlyList<T>, 
    IList<T>, 
    IList
{
    /// <summary>
    /// An empty <see cref="ImmutableEquatableArray{T}"/> instance.
    /// </summary>
    public static ImmutableEquatableArray<T> Empty => default;

    private readonly T[]? _values;
    private T[] Values => _values ?? Array.Empty<T>();

    /// <summary>
    /// Gets the element at the specified index.
    /// </summary>
    public ref readonly T this[int index] => ref Values[index];

    /// <summary>
    /// Gets the number of elements in the array.
    /// </summary>
    public int Length => _values?.Length ?? 0;

    private ImmutableEquatableArray(T[] values)
    {
        _values = values.Length == 0 ? null : values;
    }

    /// <inheritdoc/>
    public bool Equals(ImmutableEquatableArray<T> other)
    {
        if (ReferenceEquals(_values, other._values))
        {
            return true;
        }

        if (Length != other.Length)
        {
            return false;
        }

        EqualityComparer<T> comparer = EqualityComparer<T>.Default;
        for (int i = 0; i < Length; i++)
        {
            if (!comparer.Equals(Values[i], other.Values[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) 
        => obj is ImmutableEquatableArray<T> other && Equals(other);

    /// <summary>Determines whether two arrays are structurally equal.</summary>
    /// <param name="left">The first array.</param>
    /// <param name="right">The second array.</param>
    /// <returns>Whether the arrays are equal.</returns>
    public static bool operator ==(ImmutableEquatableArray<T> left, ImmutableEquatableArray<T> right) => left.Equals(right);

    /// <summary>Determines whether two arrays are structurally unequal.</summary>
    /// <param name="left">The first array.</param>
    /// <param name="right">The second array.</param>
    /// <returns>Whether the arrays are unequal.</returns>
    public static bool operator !=(ImmutableEquatableArray<T> left, ImmutableEquatableArray<T> right) => !left.Equals(right);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        int hash = 0;
        foreach (T value in Values)
        {
            hash = CommonHelpers.CombineHashCodes(hash, value is null ? 0 : value.GetHashCode());
        }

        return hash;
    }

    /// <inheritdoc/>
    public Enumerator GetEnumerator() => new(Values);

    /// <summary>
    /// Defines an enumerator for the <see cref="ImmutableEquatableArray{T}"/> type.
    /// </summary>
    public struct Enumerator : IEnumerator<T>
    {
        private readonly T[] _values;
        private int _index;

        internal Enumerator(T[] values)
        {
            _values = values;
            _index = -1;
        }

        /// <summary>
        /// Advances the enumerator to the next element of the array.
        /// </summary>
        public bool MoveNext() => ++_index < _values.Length;

        /// <summary>
        /// Gets the element at the current position of the enumerator.
        /// </summary>
        public readonly ref readonly T Current => ref _values[_index];

        readonly T IEnumerator<T>.Current => _values[_index];
        readonly object? IEnumerator.Current => _values[_index];
        void IEnumerator.Reset() => throw new NotImplementedException();
        readonly void IDisposable.Dispose() { }
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static ImmutableEquatableArray<T> UnsafeCreateFromArray(T[] values)
        => new(values);

    #region Explicit interface implementations
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => ((IEnumerable<T>)Values).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<T>)Values).GetEnumerator();
    bool ICollection<T>.IsReadOnly => true;
    bool IList.IsFixedSize => true;
    bool IList.IsReadOnly => true;
    T IReadOnlyList<T>.this[int index] => Values[index];
    T IList<T>.this[int index] { get => Values[index]; set => throw new InvalidOperationException(); }
    object? IList.this[int index] { get => Values[index]; set => throw new InvalidOperationException(); }
    void ICollection<T>.CopyTo(T[] array, int arrayIndex) => Values.CopyTo(array, arrayIndex);
    void ICollection.CopyTo(Array array, int index) => Values.CopyTo(array, index);
    int IList<T>.IndexOf(T item) => Array.IndexOf(Values, item);
    int IList.IndexOf(object value) => ((IList)Values).IndexOf(value);
    bool ICollection<T>.Contains(T item) => Array.IndexOf(Values, item) >= 0;
    bool IList.Contains(object value) => ((IList)Values).Contains(value);
    bool ICollection.IsSynchronized => true;
    object ICollection.SyncRoot => throw new NotSupportedException();

    int IReadOnlyCollection<T>.Count => Length;
    int ICollection<T>.Count => Length;
    int ICollection.Count => Length;

    void ICollection<T>.Add(T item) => throw new InvalidOperationException();
    bool ICollection<T>.Remove(T item) => throw new InvalidOperationException();
    void ICollection<T>.Clear() => throw new InvalidOperationException();
    void IList<T>.Insert(int index, T item) => throw new InvalidOperationException();
    void IList<T>.RemoveAt(int index) => throw new InvalidOperationException();
    int IList.Add(object value) => throw new InvalidOperationException();
    void IList.Clear() => throw new InvalidOperationException();
    void IList.Insert(int index, object value) => throw new InvalidOperationException();
    void IList.Remove(object value) => throw new InvalidOperationException();
    void IList.RemoveAt(int index) => throw new InvalidOperationException();
    #endregion

    private sealed class DebugView(ImmutableEquatableArray<T> array)
    {
        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public T[] Items => array.ToArray();
    }
}

/// <summary>
/// Provides extension methods for the <see cref="ImmutableEquatableArray{T}"/> type.
/// </summary>
public static class ImmutableEquatableArray
{
    /// <summary>
    /// Creates an <see cref="ImmutableEquatableArray{T}"/> instance from the specified values.
    /// </summary>
    /// <typeparam name="T">The element type for the array.</typeparam>
    /// <param name="values">The source enumerable from which to populate the array.</param>
    /// <returns>A new <see cref="ImmutableEquatableArray{T}"/> instance.</returns>
    public static ImmutableEquatableArray<T> ToImmutableEquatableArray<T>(this IEnumerable<T> values)
        => values is ICollection<T> { Count: 0 } ? ImmutableEquatableArray<T>.Empty : ImmutableEquatableArray<T>.UnsafeCreateFromArray(values.ToArray());

    /// <summary>
    /// Creates an <see cref="ImmutableEquatableArray{T}"/> instance from the specified values.
    /// </summary>
    /// <typeparam name="T">The element type for the array.</typeparam>
    /// <param name="values">The source span from which to populate the array.</param>
    /// <returns>A new <see cref="ImmutableEquatableArray{T}"/> instance.</returns>
    public static ImmutableEquatableArray<T> Create<T>(ReadOnlySpan<T> values)
        => values.IsEmpty ? ImmutableEquatableArray<T>.Empty : ImmutableEquatableArray<T>.UnsafeCreateFromArray(values.ToArray());
}
