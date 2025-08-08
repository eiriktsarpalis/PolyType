using PolyType.Abstractions;

namespace PolyType.SourceGenModel;

/// <summary>
/// Source generator model for dictionary shapes.
/// </summary>
/// <typeparam name="TDictionary">The type of the dictionary.</typeparam>
/// <typeparam name="TKey">The type of the dictionary key.</typeparam>
/// <typeparam name="TValue">The type of the dictionary value.</typeparam>
public sealed class SourceGenDictionaryTypeShape<TDictionary, TKey, TValue> : SourceGenTypeShape<TDictionary>, IDictionaryTypeShape<TDictionary, TKey, TValue>
    where TKey : notnull
{
    /// <inheritdoc/>
    public required ITypeShape<TKey> KeyType { get; init; }

    /// <inheritdoc/>
    public required ITypeShape<TValue> ValueType { get; init; }

    /// <summary>
    /// Gets the function that extracts a <see cref="IReadOnlyDictionary{TKey,TValue}"/> from an instance of the dictionary type.
    /// </summary>
    public required Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> GetDictionaryFunc { get; init; }

    /// <inheritdoc/>
    public required CollectionConstructionStrategy ConstructionStrategy { get; init; }

    /// <inheritdoc/>
    public required CollectionComparerOptions SupportedComparer { get; init; }

    /// <summary>
    /// Gets the function that constructs an empty instance of the dictionary type.
    /// </summary>
    public MutableCollectionConstructor<TKey, TDictionary>? DefaultConstructorFunc { get; init; }

    /// <summary>
    /// Gets the inserter function for adding key-value pairs to the dictionary with overwrite semantics.
    /// </summary>
    public DictionaryInserter<TDictionary, TKey, TValue>? OverwritingInserter { get; init; }

    /// <summary>
    /// Gets the inserter function for adding key-value pairs to the dictionary with discard semantics.
    /// </summary>
    public DictionaryInserter<TDictionary, TKey, TValue>? DiscardingInserter { get; init; }

    /// <summary>
    /// Gets the inserter function for adding key-value pairs to the dictionary with throwing semantics.
    /// </summary>
    public DictionaryInserter<TDictionary, TKey, TValue>? ThrowingInserter { get; init; }

    /// <summary>
    /// Gets the function that constructs a dictionary from a span of key-value pairs.
    /// </summary>
    public ParameterizedCollectionConstructor<TKey, KeyValuePair<TKey, TValue>, TDictionary>? ParameterizedConstructorFunc { get; init; }

    /// <inheritdoc/>
    public override TypeShapeKind Kind => TypeShapeKind.Dictionary;

    /// <inheritdoc/>
    public override object? Accept(TypeShapeVisitor visitor, object? state = null) => visitor.VisitDictionary(this, state);

    ITypeShape IDictionaryTypeShape.KeyType => KeyType;
    ITypeShape IDictionaryTypeShape.ValueType => ValueType;

    Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> IDictionaryTypeShape<TDictionary, TKey, TValue>.GetGetDictionary()
        => GetDictionaryFunc;

    MutableCollectionConstructor<TKey, TDictionary> IDictionaryTypeShape<TDictionary, TKey, TValue>.GetDefaultConstructor()
        => DefaultConstructorFunc ?? throw new InvalidOperationException("Dictionary shape does not specify a default constructor.");

    /// <inheritdoc/>
    public DictionaryInsertionMode AvailableInsertionModes
    {
        get
        {
            return _availableInsertionModes ??= DetermineAvailableInsertionModes();
            DictionaryInsertionMode DetermineAvailableInsertionModes()
            {
                DictionaryInsertionMode availableModes = DictionaryInsertionMode.None;
                if (OverwritingInserter is not null)
                {
                    availableModes |= DictionaryInsertionMode.Overwrite;
                }

                if (DiscardingInserter is not null)
                {
                    availableModes |= DictionaryInsertionMode.Discard;
                }

                if (ThrowingInserter is not null)
                {
                    availableModes |= DictionaryInsertionMode.Throw;
                }

                return availableModes;
            }
        }
    }

    private DictionaryInsertionMode? _availableInsertionModes;

    DictionaryInserter<TDictionary, TKey, TValue> IDictionaryTypeShape<TDictionary, TKey, TValue>.GetInserter(DictionaryInsertionMode insertionMode)
    {
        if (ConstructionStrategy is not CollectionConstructionStrategy.Mutable)
        {
            Throw();
            static void Throw() => throw new InvalidOperationException("Dictionary shape does not specify a default constructor.");
        }

        switch (insertionMode)
        {
            case DictionaryInsertionMode.None:
                return OverwritingInserter ?? DiscardingInserter ?? ThrowingInserter ?? Fail();
            case DictionaryInsertionMode.Overwrite when OverwritingInserter is { } inserter:
                return inserter;
            case DictionaryInsertionMode.Discard when DiscardingInserter is { } inserter:
                return inserter;
            case DictionaryInsertionMode.Throw when ThrowingInserter is { } inserter:
                return inserter;
            default:
                return Fail();
        }

        static DictionaryInserter<TDictionary, TKey, TValue> Fail() =>
            throw new ArgumentOutOfRangeException(nameof(insertionMode), "Unsupported dictionary insertion mode.");
    }

    ParameterizedCollectionConstructor<TKey, KeyValuePair<TKey, TValue>, TDictionary> IDictionaryTypeShape<TDictionary, TKey, TValue>.GetParameterizedConstructor()
        => ParameterizedConstructorFunc ?? throw new InvalidOperationException("Dictionary shape does not specify a span constructor.");
}