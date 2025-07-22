using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PolyType.Abstractions;

/// <summary>
/// Specifies insertion behavior for a mutable dictionary type.
/// </summary>
[Flags]
public enum DictionaryInsertionMode
{
    /// <summary>
    /// No specific insertion behavior is specified.
    /// </summary>
    /// <remarks>
    /// When passed to <see cref="IDictionaryTypeShape{TDictionary, TKey, TValue}.GetInserter(DictionaryInsertionMode)"/>,
    /// picks any available insertion method offered by the dictionary.
    /// </remarks>
    None = 0,

    /// <summary>
    /// Overwrites the existing value with the new value when the key already exists.
    /// </summary>
    /// <remarks>
    /// Canonically maps to the indexer of the dictionary.
    /// </remarks>
    Overwrite = 1,

    /// <summary>
    /// Discards the new value when the key already exists.
    /// </summary>
    /// <remarks>
    /// Canonically maps to the "TryAdd" method of the dictionary,
    /// falling back to a combination of "ContainsKey"/"Add" calls if the former is not available.
    /// </remarks>
    Discard = 2,

    /// <summary>
    /// Throws an exception when the key already exists.
    /// </summary>
    /// <remarks>
    /// Canonically maps to the "Add" method of the dictionary.
    /// </remarks>
    Throw = 4,
}
