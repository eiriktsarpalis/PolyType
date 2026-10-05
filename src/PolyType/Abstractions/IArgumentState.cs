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
    /// Gets a value indicating whether this state can be returned to an object pool.
    /// </summary>
    /// <remarks>
    /// Consumers with a shared cleanup path can use this property to skip calling
    /// <see cref="Return"/> for states that are not pooled. Calling <see cref="Return"/>
    /// unconditionally is supported; it is a no-op when this property is <see langword="false"/>.
    /// </remarks>
    bool IsPoolable { get; }

    /// <summary>
    /// Returns this argument state to its implementation for reuse.
    /// </summary>
    /// <remarks>
    /// Call this only after the state will no longer be accessed. Using the state after this
    /// method has been called is undefined behavior. Implementations that pool instances may
    /// reuse the state after this method is called. If <see cref="IsPoolable"/> is
    /// <see langword="false"/>, calling this method is a no-op.
    /// </remarks>
    void Return();
}
