using PolyType.Abstractions;
using PolyType.Utilities;
using System.Diagnostics;

namespace PolyType.Examples.DependencyInjection;

public sealed partial class ServiceProviderContext
{
    private sealed class Builder(ServiceProviderContext serviceProviderCtx, ITypeShapeFunc self) : TypeShapeVisitor, ITypeShapeFunc
    {
        private delegate void ConstructorParameterMapper<TService>(ServiceProvider serviceProvider, ref TService service);

        /// <summary>Recursively looks up or creates a factory for the specified shape.</summary>
        private ServiceFactory<TService>? GetOrAddFactory<TService>(ITypeShape<TService> shape) =>
            (ServiceFactory<TService>?)self.Invoke(shape);

        public object? Invoke<T>(ITypeShape<T> typeShape, object? state = null)
        {
            if (serviceProviderCtx._typeServiceDescriptors?.TryGetValue(typeof(T), out TypeServiceDescriptor? descriptor) is true)
            {
                state = descriptor.Lifetime;
                if (descriptor.ImplementationType != typeof(T))
                {
                    Debug.Assert(typeof(T).IsAssignableFrom(descriptor.ImplementationType));
                    ITypeShape implShape = typeShape.Provider.Resolve(descriptor.ImplementationType);
                    var otherFactory = (ServiceFactory)implShape.Invoke(this, state)!;
                    return otherFactory.Cast<T>(descriptor.Lifetime);
                }
            }

            return typeShape.Accept(this, state);
        }

        public override object? VisitObject<T>(IObjectTypeShape<T> objectShape, object? state = null)
        {
            // Only objects with constructors are supported.
            return objectShape.Constructor?.Accept(this, state);
        }

        public override object? VisitConstructor<TDeclaringType, TArgumentState>(IConstructorShape<TDeclaringType, TArgumentState> constructorShape, object? state = null)
        {
            if (constructorShape.Parameters is [])
            {
                var defaultCtor = constructorShape.GetDefaultConstructor();
                return ServiceFactory.FromFunc(_ => defaultCtor(), ResolveLifetime(state));
            }

            var argumentStateCtor = constructorShape.GetArgumentStateConstructor();
            var parameterizedCtor = constructorShape.GetParameterizedConstructor();

            ServiceLifetime lifetime = ResolveLifetime(state); // The lifetime of the service is the maximum lifetime of its dependencies.
            ConstructorParameterMapper<TArgumentState>[] parameterMappers = constructorShape.Parameters
                .Select(p =>
                {
                    var result = ((ConstructorParameterMapper<TArgumentState>? Mapper, ServiceLifetime Lifetime))p.Accept(this)!;
                    lifetime = CombineLifetimes(lifetime, result.Lifetime);
                    return result.Mapper;
                })
                .Where(mapper => mapper is not null)
                .ToArray()!;

            return ServiceFactory.FromFunc(provider =>
            {
                TArgumentState argumentState = argumentStateCtor();
                foreach (var mapper in parameterMappers)
                {
                    mapper(provider, ref argumentState);
                }
                return parameterizedCtor(ref argumentState);
            }, lifetime);
        }

        public override object? VisitConstructorParameter<TArgumentState, TParameterType>(IConstructorParameterShape<TArgumentState, TParameterType> parameterShape, object? state = null)
        {
            var parameterTypeFactory = GetOrAddFactory(parameterShape.ParameterType);
            if (parameterTypeFactory is null)
            {
                if (parameterShape.IsRequired && parameterShape.IsNonNullable)
                {
                    throw new InvalidOperationException($"No instance for the required service '{parameterShape.ParameterType.Type}' has been registered.");
                }

                return ((ConstructorParameterMapper<TArgumentState>?)null, ServiceLifetime.Singleton);
            }

            var factory = parameterTypeFactory.Factory;
            var setter = parameterShape.GetSetter();
            var mapper = new ConstructorParameterMapper<TArgumentState>((ServiceProvider provider, ref TArgumentState state) => setter(ref state, factory(provider)));
            return (mapper, parameterTypeFactory.Lifetime);
        }

        public override object? VisitDictionary<TDictionary, TKey, TValue>(IDictionaryTypeShape<TDictionary, TKey, TValue> dictionaryShape, object? state = null)
        {
            if (dictionaryShape.ConstructionStrategy is CollectionConstructionStrategy.Mutable)
            {
                Func<TDictionary> defaultCtor = dictionaryShape.GetDefaultConstructor();
                return ServiceFactory.FromFunc(_ => defaultCtor(), ResolveLifetime(state));
            }

            return null;
        }

        public override object? VisitEnumerable<TEnumerable, TElement>(IEnumerableTypeShape<TEnumerable, TElement> enumerableShape, object? state = null)
        {
            if (enumerableShape.ConstructionStrategy is CollectionConstructionStrategy.Mutable)
            {
                Func<TEnumerable> defaultCtor = enumerableShape.GetDefaultConstructor();
                return ServiceFactory.FromFunc(_ => defaultCtor(), ResolveLifetime(state));
            }

            return null;
        }

        public override object? VisitOptional<TOptional, TElement>(IOptionalTypeShape<TOptional, TElement> optionalShape, object? state)
        {
            if (GetOrAddFactory(optionalShape.ElementType) is { } elementFactory)
            {
                var lifetime = CombineLifetimes(ResolveLifetime(state), elementFactory.Lifetime);
                var createSome = optionalShape.GetSomeConstructor();
                return ServiceFactory.FromFunc<TOptional>(provider => createSome(elementFactory.Factory(provider)), ResolveLifetime(state));
            }

            return null;
        }

        public override object? VisitSurrogate<T, TSurrogate>(ISurrogateTypeShape<T, TSurrogate> surrogateShape, object? state = null)
        {
            if (GetOrAddFactory(surrogateShape.SurrogateType) is { } surrogateFactory)
            {
                var marshaller = surrogateShape.Marshaller;
                var lifetime = CombineLifetimes(ResolveLifetime(state), surrogateFactory.Lifetime);
                return ServiceFactory.FromFunc(provider => marshaller.FromSurrogate(surrogateFactory.Factory(provider)), lifetime);
            }

            return null;
        }

        public override object? VisitUnion<TUnion>(IUnionTypeShape<TUnion> unionShape, object? state = null) => null;
        public override object? VisitUnionCase<TUnionCase, TUnion>(IUnionCaseShape<TUnionCase, TUnion> unionCaseShape, object? state = null) => null;
        public override object? VisitEnum<TEnum, TUnderlying>(IEnumTypeShape<TEnum, TUnderlying> enumShape, object? state = null) => null;
        public override object? VisitProperty<TDeclaringType, TPropertyType>(IPropertyShape<TDeclaringType, TPropertyType> propertyShape, object? state = null) => null;
        private ServiceLifetime ResolveLifetime(object? state) => state is ServiceLifetime lifetime ? lifetime : serviceProviderCtx.DefaultLifetime;
        private static ServiceLifetime CombineLifetimes(ServiceLifetime serviceLifetime1, ServiceLifetime serviceLifetime2) =>
            (ServiceLifetime)Math.Max((int)serviceLifetime1, (int)serviceLifetime2);
    }

    private sealed class DelayedServiceFactoryFactory : IDelayedValueFactory
    {
        public static DelayedServiceFactoryFactory Instance { get; } = new();
        public DelayedValue Create<T>(ITypeShape<T> typeShape) =>
            new DelayedValue<ServiceFactory<T>>(self =>
                new(provider => throw new InvalidOperationException($"The dependency graph for type '{typeof(T)}' is cyclic."), ServiceLifetime.Singleton));
    }
}
