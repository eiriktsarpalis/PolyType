using PolyType.Abstractions;

namespace PolyType.Examples.DependencyInjection;

/// <summary>
/// A discriminated union encoding of service descriptors that can be registered with a <see cref="ServiceCollection"/>.
/// </summary>
internal abstract record ServiceDescriptor(Type Type, ServiceLifetime Lifetime);
internal sealed record FactoryServiceDescriptor(ServiceFactory Factory) : ServiceDescriptor(Factory.Type, Factory.Lifetime);
internal sealed record TypeServiceDescriptor(Type Type, Type ImplementationType, ServiceLifetime Lifetime) : ServiceDescriptor(Type, Lifetime);