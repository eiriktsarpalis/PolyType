using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace PolyType.Examples.DependencyInjection;

/// <summary>
/// Represents a service provider that can be used to resolve services.
/// </summary>
public sealed class ServiceProvider : IDisposable
{
    private readonly ConcurrentDictionary<Type, Lazy<object>> _serviceCache = new();

    internal ServiceProvider(ServiceProviderContext root)
    {
        Root = root;
    }

    /// <summary>
    /// Gets the root service provider context.
    /// </summary>
    public ServiceProviderContext Root { get; }

    /// <summary>
    /// Gets an instance for the specified type.
    /// </summary>
    /// <typeparam name="TService">The service type to resolve.</typeparam>
    /// <returns>The registered instance for the service.</returns>
    public bool TryGetService<TService>([NotNullWhen(true)] out TService? result) where TService : notnull
    {
        var factory = (ServiceFactory<TService>?)Root.FactoryCache.GetOrAdd(typeof(TService));
        if (factory is null)
        {
            result = default;
            return false;
        }

        result = factory.Factory(this);
        return true;
    }

    /// <summary>
    /// Gets an instance for the specified type.
    /// </summary>
    /// <typeparam name="TService">The service type to resolve.</typeparam>
    /// <returns>The registered instance for the service.</returns>
    public TService GetRequiredService<TService>() where TService : notnull
    {
        if (!TryGetService(out TService? result))
        {
            Throw();
            [DoesNotReturn] static void Throw() => throw new InvalidOperationException($"No service for type '{typeof(TService)}' has been registered.");
        }

        return result;
    }

    /// <summary>
    /// Clears all allocated resources from the current context.
    /// </summary>
    public void Clear()
    {
        foreach (Lazy<object> value in _serviceCache.Values)
        {
            if (value is { IsValueCreated: true, Value: IDisposable disposable })
            {
                disposable.Dispose();
            }
        }

        _serviceCache.Clear();
    }

    /// <summary>
    /// Gets or adds a service instance for the specified type using the specified factory.
    /// </summary>
    internal object GetOrAdd(Type type, Func<ServiceProvider, object> factory)
    {
#if NET
        return _serviceCache.GetOrAdd(type, static (_,state) => new Lazy<object>(() => state.Factory(state.Provider)), factoryArgument: (Factory: factory, Provider: this)).Value;
#else
        return _serviceCache.GetOrAdd(type, _ => new Lazy<object>(() => factory(this))).Value;
#endif
    }

    void IDisposable.Dispose() => Clear();
}
