namespace PolyType.Abstractions;

/// <summary>
/// Defines an argument state that can be returned to its implementation for reuse.
/// </summary>
/// <remarks>
/// Call <see cref="Return"/> only after the state will no longer be accessed. Using the state
/// after this method has been called is undefined behavior. If <see cref="IsPoolable"/> is
/// <see langword="false"/>, calling <see cref="Return"/> is a no-op.
/// </remarks>
[InternalImplementationsOnly]
public interface IPoolableArgumentState : IArgumentState
{
    /// <summary>
    /// Gets a value indicating whether this state can be returned to an object pool.
    /// </summary>
    bool IsPoolable { get; }

    /// <summary>
    /// Returns this argument state to its implementation for reuse.
    /// </summary>
    void Return();
}
