using PolyType.Abstractions;

namespace PolyType.Utilities;

/// <summary>
/// Performs post-hoc duplicate key detection for parameterized dictionary constructors.
/// </summary>
public static class DuplicateDictionaryKeyValidator
{
    /// <summary>
    /// Creates a new instance of <see cref="DuplicateDictionaryKeyValidator{TDictionary, TKey, TValue}"/>.
    /// </summary>
    /// <typeparam name="TDictionary">The type of the dictionary.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="dictionaryTypeShape">The dictionary type shape.</param>
    /// <param name="throwOnDuplicateKey">Function to throw an exception on duplicate key.</param>
    public static DuplicateDictionaryKeyValidator<TDictionary, TKey, TValue> CreateDuplicateKeyValidator<TDictionary, TKey, TValue>(
        this IDictionaryTypeShape<TDictionary, TKey, TValue> dictionaryTypeShape,
        Func<TKey?, Exception>? throwOnDuplicateKey = null)
        where TKey : notnull
    {
        throwOnDuplicateKey ??= key => new ArgumentException($"The key '{key}' already exists in the dictionary.", nameof(key));
        return new DuplicateDictionaryKeyValidator<TDictionary, TKey, TValue>(dictionaryTypeShape, throwOnDuplicateKey);
    }
}

/// <summary>
/// Performs post-hoc duplicate key detection for parameterized dictionary constructors.
/// </summary>
public readonly struct DuplicateDictionaryKeyValidator<TDictionary, TKey, TValue>
    where TKey : notnull
{
    private readonly Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> _getDictionary;
    private readonly Func<TKey, Exception> _throwOnDuplicateKey;

    internal DuplicateDictionaryKeyValidator(
        IDictionaryTypeShape<TDictionary, TKey, TValue> dictionaryTypeShape,
        Func<TKey, Exception> throwOnDuplicateKey)
    {
        _getDictionary = dictionaryTypeShape.GetGetDictionary();
        _throwOnDuplicateKey = throwOnDuplicateKey;
    }

    /// <summary>
    /// Checks if the final dictionary was constructed with potential duplicate key from the source buffer.
    /// </summary>
    /// <param name="finalDictionary">The constructed final dictionary.</param>
    /// <param name="sourceBuffer">The source buffer used to construct the dictionary.</param>
    /// <param name="options">The construction options used to build the dictionary.</param>
    public void ValidatePotentialDuplicates(
        in TDictionary finalDictionary,
        ReadOnlySpan<KeyValuePair<TKey, TValue>> sourceBuffer,
        CollectionConstructionOptions<TKey> options = default)
    {
        var idictionary = _getDictionary(finalDictionary);
        if (idictionary.Count < sourceBuffer.Length)
        {
            SlowIdentifyDuplicateKeyAndThrow(sourceBuffer, options);
        }
    }

    private void SlowIdentifyDuplicateKeyAndThrow(
        ReadOnlySpan<KeyValuePair<TKey, TValue>> sourceBuffer,
        CollectionConstructionOptions<TKey> options)
    {
        ISet<TKey> keys = options switch
        {
            { EqualityComparer: { } cmp } => new HashSet<TKey>(cmp),
            { Comparer: { } cmp } => new SortedSet<TKey>(cmp),
            _ => new HashSet<TKey>()
        };

        foreach (var kvp in sourceBuffer)
        {
            if (!keys.Add(kvp.Key))
            {
                throw _throwOnDuplicateKey(kvp.Key);
            }
        }

        throw new ArgumentException("The payload was found to contain duplicate keys");
    }
}
