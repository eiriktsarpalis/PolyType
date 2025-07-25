﻿using System.Globalization;

namespace PolyType.Abstractions;

/// <summary>
/// The construction strategy use for a given <see cref="IEnumerableTypeShape"/> or <see cref="IDictionaryTypeShape"/>.
/// </summary>
[Flags]
public enum CollectionConstructionStrategy
{
    /// <summary>
    /// No known construction strategy for the current collection.
    /// </summary>
    None = 0,

    /// <summary>
    /// Constructed using a default constructor and an <see cref="ICollection{T}"/>-compatible Add method.
    /// </summary>
    Mutable = 1,

    /// <summary>
    /// Constructed using a constructor or factory method accepting a <see cref="ReadOnlySpan{T}"/> of elements.
    /// </summary>
    Parameterized = 2,
}