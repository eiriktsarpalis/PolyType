﻿using Microsoft.CodeAnalysis;
using System.Collections;
using System.Collections.Immutable;

namespace PolyType.Roslyn;

/// <summary>
/// List-like data model for types implementing <see cref="IEnumerable"/> 
/// that are not dictionaries or are <see cref="Span{T}"/>, 
/// <see cref="ReadOnlySpan{T}"/>, <see cref="Memory{T}"/> or <see cref="ReadOnlyMemory{T}"/>.
/// </summary>
public sealed class EnumerableDataModel : TypeDataModel
{
    /// <inheritdoc/>
    public override TypeDataKind Kind => TypeDataKind.Enumerable;

    /// <summary>
    /// The element type of the enumerable. Typically the type parameter 
    /// of the implemented <see cref="IEnumerable{T}"/> interface or 
    /// <see cref="object"/> if using the non-generic <see cref="IEnumerable"/>.
    /// </summary>
    public required ITypeSymbol ElementType { get; init; }

    /// <summary>
    /// Gets the kind of enumerable model.
    /// </summary>
    public required EnumerableKind EnumerableKind { get; init; }

    /// <summary>
    /// Instance method used for appending an element to the collection.
    /// Implies that the collection also has an accessible default constructor.
    /// </summary>
    public required IMethodSymbol? AppendMethod { get; init; }

    /// <summary>
    /// Constructor or static factory method for the collection.
    /// </summary>
    public required IMethodSymbol? FactoryMethod { get; init; }

    /// <summary>
    /// The inferred signature of the factory method or constructor.
    /// </summary>
    public required ImmutableArray<CollectionConstructorParameter> FactorySignature { get; init; }

    /// <summary>
    /// If the enumerable is an array, the rank of the array.
    /// </summary>
    public required int Rank { get; init; }

    /// <summary>
    /// Indicates whether the enumerable is a known set type.
    /// </summary>
    public required bool IsSetType { get; init; }

    /// <summary>
    ///  Gets the insertion mode supported by the enumerable type if mutable.
    /// </summary>
    public required EnumerableInsertionMode InsertionMode { get; init; }
}

/// <summary>
/// Identifies the kind of enumerable model.
/// </summary>
public enum EnumerableKind
{
    /// <summary>
    /// Not an enumerable type.
    /// </summary>
    None,
    /// <summary>
    /// Type implementing <see cref="System.Collections.Generic.IEnumerable{T}"/>.
    /// </summary>
    IEnumerableOfT,
    /// <summary>
    /// Type implementing the non-generic <see cref="System.Collections.IEnumerable"/> interface.
    /// The value of <see cref="EnumerableDataModel.ElementType"/> is <see cref="object"/>.
    /// </summary>
    IEnumerable,
    /// <summary>
    /// An array type of rank 1.
    /// </summary>
    ArrayOfT,
    /// <summary>
    /// A <see cref="Span{T}"/> type.
    /// </summary>
    SpanOfT,
    /// <summary>
    /// A <see cref="ReadOnlySpan{T}"/> type.
    /// </summary>
    ReadOnlySpanOfT,
    /// <summary>
    /// A <see cref="Memory{T}"/> type.
    /// </summary>
    MemoryOfT,
    /// <summary>
    /// A <see cref="ReadOnlyMemory{T}"/> type.
    /// </summary>
    ReadOnlyMemoryOfT,
    /// <summary>
    /// An array of rank > 1.
    /// </summary>
    MultiDimensionalArrayOfT,
    /// <summary>
    /// An IAsyncEnumerable{T} type.
    /// </summary>
    IAsyncEnumerableOfT,
}

/// <summary>
/// Identifies the insertion models available to the dictionary.
/// </summary>
public enum EnumerableInsertionMode
{
    /// <summary>
    /// No insertion model available.
    /// </summary>
    None,

    /// <summary>
    /// Insertion possible by an Add-like method defined on the type.
    /// </summary>
    AddMethod,

    /// <summary>
    /// Enumerable explicitly implements <see cref="ICollection{T}"/>.
    /// </summary>
    ExplicitICollectionOfT,

    /// <summary>
    /// Enumerable explicitly implements <see cref="ICollection"/>.
    /// </summary>
    ExplicitIList,
}
