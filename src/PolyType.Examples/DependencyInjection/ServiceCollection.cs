using PolyType.Abstractions;

namespace PolyType.Examples.DependencyInjection;

/// <summary>
/// Represents a collection of services to be registered with a <see cref="ServiceProviderContext"/>.
/// </summary>
public sealed class ServiceCollection
{
    private const ServiceLifetime DefaultLifetime = ServiceLifetime.Scoped;
    private readonly Dictionary<Type, ServiceDescriptor> _serviceDescriptors = new();

    internal ICollection<ServiceDescriptor> ServiceDescriptors => _serviceDescriptors.Values;

    /// <summary>
    /// Creates a new <see cref="ServiceProviderContext"/> instance using the current service collection.
    /// </summary>
    /// <param name="typeShapeProvider">The shape provider governing the creation of intermediate services.</param>
    /// <param name="defaultLifetime">The default service lifetime to be used when registering or resolving services.</param>
    /// <returns>A new <see cref="ServiceProviderContext"/> instance.</returns>
    public ServiceProviderContext Build(ITypeShapeProvider typeShapeProvider, ServiceLifetime defaultLifetime = DefaultLifetime) => new(this, typeShapeProvider, defaultLifetime);

    /// <summary>
    /// Adds a service to the collection using the specified type shape provider.
    /// </summary>
    /// <typeparam name="TService">The type of the service to be registered.</typeparam>
    /// <param name="lifetime">The service lifetime for the registered instance.</param>
    /// <remarks>
    /// Uses the resolved <see cref="ITypeShape"/> to determine how the instance should be constructed and its corresponding dependencies.
    /// </remarks>
    public void Add<TService>(ServiceLifetime lifetime = DefaultLifetime) where TService : notnull
    {
        _serviceDescriptors.Add(typeof(TService), new TypeServiceDescriptor(typeof(TService), typeof(TService), lifetime));
    }

    /// <summary>
    /// Adds a service to the collection using specified implementation type and type shape provider.
    /// </summary>
    /// <typeparam name="TService">The type of the service to be registered.</typeparam>
    /// <typeparam name="TImplementation">The type of the implementation to be registered.</typeparam>
    /// <param name="lifetime">The service lifetime for the registered instance.</param>
    /// <remarks>
    /// Uses the resolved <see cref="ITypeShape"/> to determine how the instance should be constructed and its corresponding dependencies.
    /// </remarks>
    public void Add<TService, TImplementation>(ServiceLifetime lifetime = DefaultLifetime)
        where TService : class
        where TImplementation : TService
    {
        _serviceDescriptors.Add(typeof(TService), new TypeServiceDescriptor(typeof(TService), typeof(TImplementation), lifetime));
    }

    /// <summary>
    /// Adds a service to the collection using the specified value.
    /// </summary>
    /// <typeparam name="TService">The type of the service to be registered.</typeparam>
    /// <param name="value">The singleton value to be registered for the service.</param>
    public void Add<TService>(TService value) where TService : notnull
    {
        ServiceFactory<TService> factory = ServiceFactory.FromValue(value);
        _serviceDescriptors.Add(typeof(TService), new FactoryServiceDescriptor(factory));
    }

    /// <summary>
    /// Adds a service to the collection using the specified value factory.
    /// </summary>
    /// <typeparam name="TService">The type of the service to be registered.</typeparam>
    /// <param name="factory">The factory used to create instances of the service.</param>
    /// <param name="lifetime">The lifetime to be registered for the service.</param>
    public void Add<TService>(Func<ServiceProvider, TService> factory, ServiceLifetime lifetime = DefaultLifetime) where TService : notnull
    {
        ServiceFactory serviceFactory = ServiceFactory.FromFunc(factory, lifetime);
        _serviceDescriptors.Add(typeof(TService), new FactoryServiceDescriptor(serviceFactory));
    }
}
