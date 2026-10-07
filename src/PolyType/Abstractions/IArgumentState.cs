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

    /// <summary>
    /// Returns this argument state to its implementation for reuse.
    /// </summary>
    /// <remarks>
    /// Call this method exactly once after the state will no longer be accessed, including
    /// when construction or invocation throws. Accessing a state after returning it is
    /// undefined behavior. Implementations that do not pool states treat this as a no-op.
    /// </remarks>
    void Return();
}
