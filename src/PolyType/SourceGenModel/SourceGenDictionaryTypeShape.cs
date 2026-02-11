using PolyType.Abstractions;
using System.Diagnostics;

namespace PolyType.SourceGenModel;

/// <summary>
/// Source generator model for dictionary shapes.
/// </summary>
/// <typeparam name="TDictionary">The type of the dictionary.</typeparam>
/// <typeparam name="TKey">The type of the dictionary key.</typeparam>
/// <typeparam name="TValue">The type of the dictionary value.</typeparam>
[DebuggerTypeProxy(typeof(PolyType.Debugging.DictionaryTypeShapeDebugView))]
public sealed class SourceGenDictionaryTypeShape<TDictionary, TKey, TValue> : SourceGenTypeShape<TDictionary>, IDictionaryTypeShape<TDictionary, TKey, TValue>
    where TKey : notnull
{
    /// <summary>
    /// Gets a delayed key type shape factory for use with potentially recursive type graphs.
    /// </summary>
    public Func<ITypeShape<TKey>>? KeyTypeFunc { get; init; }

    /// <inheritdoc/>
    [Obsolete("Use KeyTypeFunc for delayed initialization to avoid stack overflows with recursive types.")]
    public ITypeShape<TKey> KeyType
    {
        get => _keyType ??= KeyTypeFunc?.Invoke() ?? throw new InvalidOperationException("KeyTypeFunc has not been initialized.");
        init => _keyType = value;
    }

    private ITypeShape<TKey>? _keyType;

    /// <summary>
    /// Gets a delayed value type shape factory for use with potentially recursive type graphs.
    /// </summary>
    public Func<ITypeShape<TValue>>? ValueTypeFunc { get; init; }

    /// <inheritdoc/>
    [Obsolete("Use ValueTypeFunc for delayed initialization to avoid stack overflows with recursive types.")]
    public ITypeShape<TValue> ValueType
    {
        get => _valueType ??= ValueTypeFunc?.Invoke() ?? throw new InvalidOperationException("ValueTypeFunc has not been initialized.");
        init => _valueType = value;
    }

    private ITypeShape<TValue>? _valueType;

    /// <summary>
    /// Gets the function that extracts a <see cref="IReadOnlyDictionary{TKey,TValue}"/> from an instance of the dictionary type.
    /// </summary>
    public required Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> GetDictionary { get; init; }

    /// <inheritdoc/>
    public required CollectionConstructionStrategy ConstructionStrategy { get; init; }

    /// <inheritdoc/>
    public required CollectionComparerOptions SupportedComparer { get; init; }

    /// <summary>
    /// Gets the function that constructs an empty instance of the dictionary type.
    /// </summary>
    public MutableCollectionConstructor<TKey, TDictionary>? DefaultConstructor { get; init; }

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
    public ParameterizedCollectionConstructor<TKey, KeyValuePair<TKey, TValue>, TDictionary>? ParameterizedConstructor { get; init; }

    /// <inheritdoc/>
    public override TypeShapeKind Kind => TypeShapeKind.Dictionary;

    /// <inheritdoc/>
    public override object? Accept(TypeShapeVisitor visitor, object? state = null) => visitor.VisitDictionary(this, state);

#pragma warning disable CS0618 // Type or member is obsolete -- used internally for interface implementation
    ITypeShape IDictionaryTypeShape.KeyType => KeyType;
    ITypeShape IDictionaryTypeShape.ValueType => ValueType;
#pragma warning restore CS0618

    Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> IDictionaryTypeShape<TDictionary, TKey, TValue>.GetGetDictionary() => GetDictionary;

    MutableCollectionConstructor<TKey, TDictionary> IDictionaryTypeShape<TDictionary, TKey, TValue>.GetDefaultConstructor() =>
        DefaultConstructor ?? throw new InvalidOperationException("Dictionary shape does not specify a default constructor.");

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

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
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

    ParameterizedCollectionConstructor<TKey, KeyValuePair<TKey, TValue>, TDictionary> IDictionaryTypeShape<TDictionary, TKey, TValue>.GetParameterizedConstructor() =>
        ParameterizedConstructor ?? throw new InvalidOperationException("Dictionary shape does not specify a span constructor.");
}