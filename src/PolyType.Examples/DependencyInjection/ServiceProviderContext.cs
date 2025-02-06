using PolyType.Abstractions;
using PolyType.Utilities;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace PolyType.Examples.DependencyInjection;

/// <summary>
/// Represents a global context for creating and managing services.
/// </summary>
public sealed partial class ServiceProviderContext : IDisposable
{
    private readonly Dictionary<Type, TypeServiceDescriptor>? _typeServiceDescriptors;
    private ServiceProvider? _singletonProvider;

    /// <summary>
    /// Creates a new service context with the specified service collection, type shape provider, and default lifetime.
    /// </summary>
    /// <param name="serviceCollection">The service collection consulted by the provider.</param>
    /// <param name="typeshapeProvider">The shape provider used to resolve type graphs.</param>
    /// <param name="defaultLifetime">The default service lifetime to be used by instantiated services.</param>
    public ServiceProviderContext(ServiceCollection serviceCollection, ITypeShapeProvider typeshapeProvider, ServiceLifetime defaultLifetime)
    {
        TypeShapeProvider = typeshapeProvider;
        DefaultLifetime = defaultLifetime;
        FactoryCache = new(typeshapeProvider)
        {
            ValueBuilderFactory = context => new Builder(this, context),
            DelayedValueFactory = DelayedServiceFactoryFactory.Instance,
        };

        foreach (ServiceDescriptor serviceDescriptor in serviceCollection.ServiceDescriptors)
        {
            switch (serviceDescriptor)
            {
                case FactoryServiceDescriptor factoryDescriptor:
                    FactoryCache[factoryDescriptor.Type] = factoryDescriptor.Factory;
                    break;

                case TypeServiceDescriptor typeServiceDescriptor:
                    (_typeServiceDescriptors ??= new()).Add(typeServiceDescriptor.Type, typeServiceDescriptor);
                    break;

                default:
                    Debug.Fail($"Unknown service descriptor type: {serviceDescriptor.GetType()}");
                    break;
            }
        }
    }

    /// <summary>Gets the type shape provider used to resolve type shapes for registered services.</summary>
    public ITypeShapeProvider TypeShapeProvider { get; }

    /// <summary>The default service lifetime to be used when registering or resolving services.</summary>
    public ServiceLifetime DefaultLifetime { get; }

    internal TypeCache FactoryCache { get; }
    internal ServiceProvider SingletonProvider => _singletonProvider ??= new(this);

    /// <summary>
    /// Creates a new <see cref="ServiceProvider"/> instance using the current service context.
    /// </summary>
    /// <returns>A new <see cref="ServiceProvider"/> instance that can be used to resolve services.</returns>
    public ServiceProvider CreateScope() => new(this);

    /// <summary>
    /// Disposes of the service context and its allocated resources.
    /// </summary>
    public void Dispose() => ((IDisposable?)_singletonProvider)?.Dispose();
}
