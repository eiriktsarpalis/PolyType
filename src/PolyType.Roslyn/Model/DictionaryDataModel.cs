﻿using Microsoft.CodeAnalysis;
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
    /// <see langword="true"/> if the dictionary type only exposes an indexer via an explicit interface implementation of either <see cref="IDictionary{TKey, TValue}"/> or <see cref="IDictionary"/>.
    /// </summary>
    public required bool IndexerIsExplicitInterfaceImplementation { get; init; }
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