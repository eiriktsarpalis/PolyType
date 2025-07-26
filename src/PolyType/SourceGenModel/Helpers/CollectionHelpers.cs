using PolyType.Abstractions;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PolyType.SourceGenModel;

/// <summary>
/// Collection helper methods to be consumed by the source generator.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class CollectionHelpers
{
    /// <summary>
    /// Creates a list from a <see cref="ReadOnlySpan{T}"/>.
    /// </summary>
    /// <typeparam name="T">The element type of the list.</typeparam>
    /// <param name="span">The span containing the elements of the list.</param>
    /// <returns>A new list containing the specified elements.</returns>
    public static List<T> CreateList<T>(ReadOnlySpan<T> span)
    {
        var list = new List<T>(span.Length);
#if NET
        CollectionsMarshal.SetCount(list, span.Length);
        span.CopyTo(CollectionsMarshal.AsSpan(list));
#else
        for (int i = 0; i < span.Length; i++)
        {
            list.Add(span[i]);
        }
#endif
        return list;
    }

    /// <summary>
    /// Creates a set from a <see cref="ReadOnlySpan{T}"/>.
    /// </summary>
    /// <typeparam name="T">The element type of the set.</typeparam>
    /// <param name="span">The span containing the elements of the set.</param>
    /// <param name="comparer">An optional comparer for the returned set.</param>
    /// <returns>A new set containing the specified elements.</returns>
    public static HashSet<T> CreateHashSet<T>(ReadOnlySpan<T> span, IEqualityComparer<T>? comparer = null)
    {
        HashSet<T> set =
#if NET
            new(span.Length, comparer);
#else
            new(comparer);
#endif

        for (int i = 0; i < span.Length; i++)
        {
            set.Add(span[i]); // NB duplicates have overwrite semantics.
        }

        return set;
    }

    /// <summary>
    /// Creates a dictionary from a span of key/value pairs.
    /// </summary>
    /// <typeparam name="TKey">The key type of the dictionary.</typeparam>
    /// <typeparam name="TValue">The value type of the dictionary.</typeparam>
    /// <param name="span">The span containing the entries of the dictionary.</param>
    /// <param name="keyComparer">An optional key comparer for the returned dictionary.</param>
    /// <returns>A new dictionary containing the specified entries.</returns>
    public static Dictionary<TKey, TValue> CreateDictionary<TKey, TValue>(ReadOnlySpan<KeyValuePair<TKey, TValue>> span, IEqualityComparer<TKey>? keyComparer = null)
        where TKey : notnull
    {
        var dict = new Dictionary<TKey, TValue>(span.Length, keyComparer);
        for (int i = 0; i < span.Length; i++)
        {
            KeyValuePair<TKey, TValue> kvp = span[i];
            dict[kvp.Key] = kvp.Value; // NB duplicate keys have overwrite semantics.
        }

        return dict;
    }

    /// <summary>
    /// Creates an array of tuples from a span of key/value pairs.
    /// </summary>
    /// <typeparam name="TKey">The key type of the span.</typeparam>
    /// <typeparam name="TValue">The value type of the span.</typeparam>
    /// <param name="span">The span containing the entries of the span.</param>
    /// <returns>A new array containing the specified entries.</returns>
    public static Tuple<TKey, TValue>[] CreateTupleArray<TKey, TValue>(ReadOnlySpan<KeyValuePair<TKey, TValue>> span)
    {
        Tuple<TKey, TValue>[] array = new Tuple<TKey, TValue>[span.Length];
        for (int i = 0; i < span.Length; i++)
        {
            var kvp = span[i];
            array[i] = Tuple.Create(kvp.Key, kvp.Value);
        }

        return array;
    }

    /// <summary>
    /// Creates a <see cref="IReadOnlyDictionary{TKey, TValue}"/> adapter for a <see cref="IDictionary{TKey, TValue}"/>.
    /// </summary>
    /// <typeparam name="TDictionary">The dictionary type to be wrapped.</typeparam>
    /// <typeparam name="TKey">The key type of the dictionary.</typeparam>
    /// <typeparam name="TValue">The value type of the dictionary.</typeparam>
    /// <param name="dictionary">The source dictionary to be wrapped.</param>
    /// <returns>A read-only dictionary instance wrapping the source dictionary.</returns>
    public static IReadOnlyDictionary<TKey, TValue> AsReadOnlyDictionary<TDictionary, TKey, TValue>(TDictionary dictionary)
        where TDictionary : IDictionary<TKey, TValue>
        => dictionary is IReadOnlyDictionary<TKey, TValue> rod ? rod : new ReadOnlyDictionaryAdapter<TDictionary, TKey, TValue>(dictionary);

    /// <summary>
    /// Creates a <see cref="IReadOnlyDictionary{TKey, TValue}"/> adapter for a <see cref="IDictionary"/>.
    /// </summary>
    /// <typeparam name="TDictionary">The dictionary type to be wrapped.</typeparam>
    /// <param name="dictionary">The source dictionary to be wrapped.</param>
    /// <returns>A read-only dictionary instance wrapping the source dictionary.</returns>
    public static IReadOnlyDictionary<object, object?> AsReadOnlyDictionary<TDictionary>(TDictionary dictionary)
        where TDictionary : IDictionary
        => dictionary is IReadOnlyDictionary<object, object?> rod ? rod : new ReadOnlyDictionaryAdapter<TDictionary>(dictionary);

    /// <summary>
    /// Creates an element insertion delegate for collections that implement <see cref="ICollection{T}"/>.
    /// </summary>
    /// <typeparam name="TEnumerable">The collection type that implements <see cref="ICollection{T}"/>.</typeparam>
    /// <typeparam name="TElement">The element type of the collection.</typeparam>
    /// <returns>A delegate that adds elements to the collection.</returns>
    public static EnumerableAppender<TEnumerable, TElement> CreateEnumerableAppender<TEnumerable, TElement>()
        where TEnumerable : ICollection<TElement>
    {
        return (ref TEnumerable enumerable, TElement element) =>
        {
            enumerable.Add(element);
            return true;
        };
    }

    /// <summary>
    /// Creates an element insertion delegate for collections that implement <see cref="IList"/>.
    /// </summary>
    /// <typeparam name="TEnumerable">The collection type that implements <see cref="IList"/>.</typeparam>
    /// <returns>A delegate that adds elements to the collection.</returns>
    public static EnumerableAppender<TEnumerable, object?> CreateEnumerableAppender<TEnumerable>()
        where TEnumerable : IList
    {
        return (ref TEnumerable enumerable, object? element) =>
        {
            enumerable.Add(element);
            return true;
        };
    }

    /// <summary>
    /// Creates a dictionary insertion delegate based on the available APIs on <see cref="IDictionary{TKey, TValue}"/>.
    /// </summary>
    /// <typeparam name="TDictionary">The dictionary type that implements <see cref="IDictionary{TKey, TValue}"/>.</typeparam>
    /// <typeparam name="TKey">The key type of the dictionary.</typeparam>
    /// <typeparam name="TValue">The value type of the dictionary.</typeparam>
    /// <param name="insertionMode">The insertion mode that determines the behavior when a key already exists.</param>
    /// <returns>A delegate that inserts key-value pairs into the dictionary according to the specified insertion mode.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when an invalid <paramref name="insertionMode"/> is specified.</exception>
    public static DictionaryInserter<TDictionary, TKey, TValue> CreateDictionaryInserter<TDictionary, TKey, TValue>(DictionaryInsertionMode insertionMode)
        where TDictionary : IDictionary<TKey, TValue>
    {
        return insertionMode switch
        {
            DictionaryInsertionMode.Overwrite => static (ref TDictionary dictionary, TKey key, TValue value) =>
            {
                dictionary[key] = value;
                return true;
            },

            DictionaryInsertionMode.Discard => static (ref TDictionary dictionary, TKey key, TValue value) =>
            {
                if (dictionary.ContainsKey(key))
                {
                    return false;
                }

                dictionary[key] = value;
                return true;
            },

            DictionaryInsertionMode.Throw => static (ref TDictionary dictionary, TKey key, TValue value) =>
            {
                dictionary.Add(key, value);
                return true;
            },

            _ => throw new ArgumentOutOfRangeException(nameof(insertionMode)),
        };
    }

    /// <summary>
    /// Creates a dictionary insertion delegate based on the available APIs on <see cref="IDictionary"/>.
    /// </summary>
    /// <typeparam name="TDictionary">The dictionary type that implements <see cref="IDictionary"/>.</typeparam>
    /// <param name="insertionMode">The insertion mode that determines the behavior when a key already exists.</param>
    /// <returns>A delegate that inserts key-value pairs into the dictionary according to the specified insertion mode.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when an invalid <paramref name="insertionMode"/> is specified.</exception>
    public static DictionaryInserter<TDictionary, object, object?> CreateDictionaryInserter<TDictionary>(DictionaryInsertionMode insertionMode)
        where TDictionary : IDictionary
    {
        return insertionMode switch
        {
            DictionaryInsertionMode.Overwrite => static (ref TDictionary dictionary, object key, object? value) =>
            {
                dictionary[key] = value;
                return true;
            },

            DictionaryInsertionMode.Discard => static (ref TDictionary dictionary, object key, object? value) =>
            {
                if (dictionary.Contains(key))
                {
                    return false;
                }

                dictionary[key] = value;
                return true;
            },

            DictionaryInsertionMode.Throw => static (ref TDictionary dictionary, object key, object? value) =>
            {
                dictionary.Add(key, value);
                return true;
            },

            _ => throw new ArgumentOutOfRangeException(nameof(insertionMode)),
        };
    }

    private sealed class ReadOnlyDictionaryAdapter<TDictionary, TKey, TValue> : IReadOnlyDictionary<TKey, TValue>
        where TDictionary : IDictionary<TKey, TValue>
    {
        private readonly TDictionary _dictionary;
        public ReadOnlyDictionaryAdapter(TDictionary dictionary)
        {
            Debug.Assert(dictionary is not IReadOnlyDictionary<TKey, TValue>);
            _dictionary = dictionary;
        }

        public int Count => _dictionary.Count;
        public TValue this[TKey key] => _dictionary[key];
        public IEnumerable<TKey> Keys => _dictionary.Keys;
        public IEnumerable<TValue> Values => _dictionary.Values;
        public bool ContainsKey(TKey key) => _dictionary.ContainsKey(key);
        public bool TryGetValue(TKey key, out TValue value) => _dictionary.TryGetValue(key, out value!);
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => _dictionary.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class ReadOnlyDictionaryAdapter<TDictionary> : IReadOnlyDictionary<object, object?>
        where TDictionary : IDictionary
    {
        private readonly TDictionary _dictionary;

        public ReadOnlyDictionaryAdapter(TDictionary dictionary)
        {
            Debug.Assert(dictionary is not IReadOnlyDictionary<object, object?>);
            _dictionary = dictionary;
        }

        public int Count => _dictionary.Count;
        public IEnumerable<object> Keys => _dictionary.Keys.Cast<object>();
        public IEnumerable<object?> Values => _dictionary.Values.Cast<object?>();
        public object? this[object key] => _dictionary[key];
        public bool ContainsKey(object key) => _dictionary.Contains(key);

        public bool TryGetValue(object key, out object? value)
        {
            if (_dictionary.Contains(key))
            {
                value = _dictionary[key];
                return true;
            }
            else
            {
                value = null;
                return false;
            }
        }

        public IEnumerator<KeyValuePair<object, object?>> GetEnumerator()
        {
            foreach (DictionaryEntry entry in _dictionary)
            {
                yield return new(entry.Key, entry.Value);
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
