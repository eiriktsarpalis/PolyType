using System.Diagnostics;

namespace PolyType.Examples.DependencyInjection;

/// <summary>
/// Defines an abstract factory for getting or creating service instances.
/// </summary>
internal sealed class ServiceFactory<T>(Func<ServiceProvider, T> factory, ServiceLifetime lifetime, Func<ServiceProvider, T>? underlyingFactory = null)
    : ServiceFactory(typeof(T), lifetime)
{
    private readonly Func<ServiceProvider, T> _underlyingFactory = underlyingFactory ?? factory;
    public Func<ServiceProvider, T> Factory { get; } = factory;
    public override ServiceFactory<TBase> Cast<TBase>(ServiceLifetime targetLifetime)
    {
        Debug.Assert(typeof(TBase).IsAssignableFrom(typeof(T)));
        var castUnderlyingFactory = (Func<ServiceProvider, TBase>)(object)_underlyingFactory;
        var castFactory = Lifetime > targetLifetime
            ? ApplyLifetime(castUnderlyingFactory, targetLifetime)
            : (Func<ServiceProvider, TBase>)(object)Factory;

        return new(castFactory, targetLifetime, castUnderlyingFactory);
    }
}

/// <summary>
/// Defines an abstract factory for getting or creating service instances.
/// </summary>
internal abstract class ServiceFactory(Type type, ServiceLifetime lifetime)
{
    public Type Type { get; } = type;
    public ServiceLifetime Lifetime { get; } = lifetime;
    public abstract ServiceFactory<TBase> Cast<TBase>(ServiceLifetime targetLifetime);
    public static ServiceFactory<T> FromValue<T>(T value) => new(_ => value, ServiceLifetime.Singleton);
    public static ServiceFactory<T> FromFunc<T>(Func<ServiceProvider, T> factory, ServiceLifetime lifeTime) =>
        new(ApplyLifetime(factory, lifeTime), lifeTime, factory);

    protected static Func<ServiceProvider, T> ApplyLifetime<T>(Func<ServiceProvider, T> factory, ServiceLifetime targetLifetime)
    {
        Func<ServiceProvider, object> untypedFactory;
        switch (targetLifetime)
        {
            case ServiceLifetime.Singleton:
                untypedFactory = Upcast(factory);
                return serviceProvider => (T)serviceProvider.Root.SingletonProvider.GetOrAdd(typeof(T), untypedFactory);

            case ServiceLifetime.Scoped:
                untypedFactory = Upcast(factory);
                return serviceProvider => (T)serviceProvider.GetOrAdd(typeof(T), untypedFactory);

            default:
                Debug.Assert(targetLifetime is ServiceLifetime.Transient);
                return factory;
        }

        static Func<ServiceProvider, object> Upcast(Func<ServiceProvider, T> func)
        {
            return typeof(T).IsValueType
                ? provider => func(provider)!
                : (Func<ServiceProvider, object>)(object)func;
        }
    }
}