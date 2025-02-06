namespace PolyType.Examples.DependencyInjection;

/// <summary>
/// Defines the service lifetimes. Scoped lifetimes have been omitted for simplicity.
/// </summary>
public enum ServiceLifetime
{
    /// <summary>
    /// Specifies that a single instance of the service will be created.
    /// </summary>
    Singleton,

    /// <summary>
    /// Specifies that a new instance of the service will be created for each scope.
    /// </summary>
    Scoped,

    /// <summary>
    /// Specifies that a new instance of the service will be created each time it is requested.
    /// </summary>
    Transient,
}
