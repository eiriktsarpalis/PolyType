using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PolyType.Abstractions;

/// <summary>
/// Declares list of arguments to be passed to a parameterized constructor or method.
/// </summary>
[InternalImplementationsOnly]
public interface IArgumentState
{
    /// <summary>
    /// Gets the total number of arguments expected by the constructor.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Gets a value indicating whether all required arguments have been set.
    /// </summary>
    bool AreRequiredArgumentsSet { get; }

    /// <summary>
    /// Checks if the argument at the specified index is set.
    /// </summary>
    /// <param name="index">The index of the argument to check.</param>
    /// <returns>True if the argument is set; otherwise, false.</returns>
    bool IsArgumentSet(int index);
}
