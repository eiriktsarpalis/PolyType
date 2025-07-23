using Microsoft.CodeAnalysis;
using System.Collections;
using System.Collections.Immutable;

namespace PolyType.Roslyn;

/// <summary>
/// Dictionary data model for types implementing <see cref="IDictionary{TKey, TValue}"/>,
/// <see cref="IReadOnlyDictionary{TKey, TValue}"/> or <see cref="System.Collections.IDictionary"/>.
/// </summary>
public sealed class DictionaryDataModel : TypeDataModel
{
    /// <inheritdoc/>
    public override TypeDataKind Kind => TypeDataKind.Dictionary;

    /// <summary>
    /// The type of key used by the dictionary. 
    /// Is <see cref="object"/> if implementing <see cref="IDictionary"/>.
    /// </summary>
    public required ITypeSymbol KeyType { get; init; }

    /// <summary>
    /// The type of value used by the dictionary. 
    /// Is <see cref="object"/> if implementing <see cref="IDictionary"/>.
    /// </summary>
    public required ITypeSymbol ValueType { get; init; }

    /// <summary>
    /// Gets the kind of the current dictionary model.
    /// </summary>
    public required DictionaryKind DictionaryKind { get; init; }

    /// <summary>
    /// Constructor or static factory method.
    /// </summary>
    public required IMethodSymbol? FactoryMethod { get; init; }

    /// <summary>
    /// The inferred signature of the factory method or constructor.
    /// </summary>
    public required ImmutableArray<CollectionConstructorParameter> FactorySignature { get; init; }

    /// <summary>
    /// Gets the available insertion modes supported by the dictionary type if mutable.
    /// </summary>
    public required DictionaryInsertionMode AvailableInsertionModes { get; init; }
}

/// <summary>
/// Identifies the kind of dictionary model.
/// </summary>
public enum DictionaryKind
{
    /// <summary>
    /// The type implements <see cref="IDictionary{TKey, TValue}"/>.
    /// </summary>
    IDictionaryOfKV,

    /// <summary>
    /// The type implements <see cref="IReadOnlyDictionary{TKey, TValue}"/>.
    /// </summary>
    IReadOnlyDictionaryOfKV,

    /// <summary>
    /// The type implements <see cref="System.Collections.IDictionary"/>.
    /// </summary>
    IDictionary,
}

/// <summary>
/// Identifies the insertion models available to the dictionary.
/// </summary>
[Flags]
public enum DictionaryInsertionMode
{
    /// <summary>
    /// No available insertion mode.
    /// </summary>
    None = 0,

    /// <summary>
    /// Dictionary exposes a settable indexer.
    /// </summary>
    SetItem = 1,

    /// <summary>
    /// Dictionary exposes a "TryAdd" method,
    /// </summary>
    TryAdd = 2,

    /// <summary>
    /// Dictionary exposes an "Add" method.
    /// </summary>
    Add = 4,

    /// <summary>
    /// Dictionary supports emulating "TryAdd" using a combination of "ContainsKey" and "Add".
    /// </summary>
    ContainsKeyAdd = 8,

    /// <summary>
    /// Dictionary explicitly implements <see cref="IDictionary{TKey, TValue}"/>.
    /// </summary>
    ExplicitIDictionaryOfT = 16,

    /// <summary>
    /// Dictionary explicitly implements <see cref="IDictionary"/>.
    /// </summary>
    ExplicitIDictionary = 32,
}