using PolyType.Abstractions;
using PolyType.Examples.Utilities;
using PolyType.Utilities;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace PolyType.Examples.ObjectMapper;

public static partial class Mapper
{
    private delegate void PropertyMapper<TSource, TTarget>(ref TSource source, ref TTarget target);

    private sealed class Builder(ITypeShapeFunc self) : MapperTypeShapeVisitor, ITypeShapeFunc
    {
        /// <summary>Recursively looks up or creates a mapper for the specified shapes.</summary>
        public Mapper<TSource, TTarget> GetOrAddMapper<TSource, TTarget>(ITypeShape<TSource> fromShape, ITypeShape<TTarget> toShape)
        {
            ITypeShape<Mapper<TSource, TTarget>> mapperShape = new MapperShape<TSource, TTarget>(fromShape, toShape);
            return (Mapper<TSource, TTarget>)self.Invoke(mapperShape)!;
        }

        object? ITypeShapeFunc.Invoke<T>(ITypeShape<T> typeShape, object? state) => typeShape.Accept(this, state);

        public override object? VisitMapper<TSource, TTarget>(ITypeShape<TSource> sourceShape, ITypeShape<TTarget> targetShape, object? state)
        {
            if (sourceShape.Kind != targetShape.Kind)
            {
                // For simplicity, only map between types of matching kind.
                ThrowCannotMapTypes(typeof(TSource), typeof(TTarget));
            }

            switch (sourceShape.Kind)
            {
                case TypeShapeKind.Object:
                    var sourceObjectShape = (IObjectTypeShape<TSource>)sourceShape;
                    var targetObjectShape = (IObjectTypeShape<TTarget>)targetShape;

                    if (targetObjectShape.Constructor is null)
                    {
                        // If TTarget is not constructible, only map if TSource is a subtype of TTarget and has no properties.
                        if (typeof(TTarget).IsAssignableFrom(typeof(TSource)) && targetObjectShape.Properties is [])
                        {
                            return new Mapper<TSource, TSource>(source => source);

                        }

                        ThrowCannotMapTypes(typeof(TSource), typeof(TTarget));
                    }

                    IPropertyShape[] sourceGetters = sourceObjectShape.Properties
                        .Where(prop => prop.HasGetter)
                        .ToArray();

                    // Bring TSource into scope for the target ctor using a new generic visitor.
                    var visitor = new TypeScopedVisitor<TSource>(this);
                    return (Mapper<TSource, TTarget>?)targetObjectShape.Constructor.Accept(visitor, state: sourceGetters);

                case TypeShapeKind.Enum:
                    return new Mapper<TSource, TTarget>(source => (TTarget)(object)source!);

                default:
                    return (Mapper<TSource, TTarget>?)sourceShape.Accept(this, state: targetShape);
            }
        }

        public override object? VisitProperty<TSource, TSourceProperty>(IPropertyShape<TSource, TSourceProperty> sourceGetter, object? state = null)
        {
            DebugExt.Assert(state is IPropertyShape or IParameterShape);
            var visitor = new PropertyScopedVisitor<TSource, TSourceProperty>(this);
            return state is IPropertyShape targetProp
                ? targetProp.Accept(visitor, sourceGetter)
                : ((IParameterShape)state).Accept(visitor, state: sourceGetter);
        }

        public override object? VisitOptional<TOptional, TElement>(IOptionalTypeShape<TOptional, TElement> optionalShape, object? state)
        {
            var targetNullable = (IOptionalTypeShape)state!;
            var visitor = new NullableScopedVisitor<TOptional, TElement>(this);
            return targetNullable.Accept(visitor, state: optionalShape);
        }

        public override object? VisitSurrogate<T, TSurrogate>(ISurrogateTypeShape<T, TSurrogate> surrogateShape, object? state = null)
        {
            var targetShape = (ISurrogateTypeShape)state!;
            var visitor = new SurrogateScopedVisitor<T, TSurrogate>(this);
            return targetShape.Accept(visitor, state: surrogateShape);
        }

        public override object? VisitEnumerable<TSource, TSourceElement>(IEnumerableTypeShape<TSource, TSourceElement> enumerableShape, object? state)
        {
            var targetEnumerable = (IEnumerableTypeShape)state!;
            var visitor = new EnumerableScopedVisitor<TSource, TSourceElement>(this);
            return targetEnumerable.Accept(visitor, state: enumerableShape);
        }

        public override object? VisitDictionary<TSourceDictionary, TSourceKey, TSourceValue>(IDictionaryTypeShape<TSourceDictionary, TSourceKey, TSourceValue> sourceDictionary, object? state)
        {
            var targetDictionary = (IDictionaryTypeShape)state!;
            var visitor = new DictionaryScopedVisitor<TSourceDictionary, TSourceKey, TSourceValue>(this);
            return targetDictionary.Accept(visitor, state: sourceDictionary);
        }

        private sealed class TypeScopedVisitor<TSource>(Builder baseVisitor) : TypeShapeVisitor
        {
            public override object? VisitConstructor<TTarget, TArgumentState>(IConstructorShape<TTarget, TArgumentState> targetCtor, object? state)
            {
                var sourceGetters = (IPropertyShape[])state!;
                if (targetCtor.Parameters is [])
                {
                    Func<TTarget> defaultCtor = targetCtor.GetDefaultConstructor();
                    PropertyMapper<TSource, TTarget>[] propertyMappers = targetCtor.DeclaringType.Properties
                        .Where(prop => prop.HasSetter)
                        .Select(setter =>
                            sourceGetters.FirstOrDefault(getter => getter.Name == setter.Name) is { } getter
                            ? (PropertyMapper<TSource, TTarget>?)getter.Accept(baseVisitor, state: setter)
                            : null)
                        .Where(mapper => mapper != null)
                        .ToArray()!;

                    return new Mapper<TSource, TTarget>(source =>
                    {
                        if (source is null)
                        {
                            return default;
                        }

                        TTarget target = defaultCtor();
                        foreach (PropertyMapper<TSource, TTarget> mapper in propertyMappers)
                        {
                            mapper(ref source, ref target);
                        }

                        return target;
                    });
                }
                else
                {
                    Func<TArgumentState> argumentStateCtor = targetCtor.GetArgumentStateConstructor();
                    Constructor<TArgumentState, TTarget> ctor = targetCtor.GetParameterizedConstructor();
                    PropertyMapper<TSource, TArgumentState>[] propertyMappers = targetCtor.Parameters
                        .Select(targetParam =>
                        {
                            // Use case-insensitive comparison for constructor parameters, but case-sensitive for members.
                            StringComparison comparison = targetParam.Kind is ParameterKind.MethodParameter
                                ? StringComparison.OrdinalIgnoreCase
                                : StringComparison.Ordinal;

                            var mapper = sourceGetters.FirstOrDefault(getter => string.Equals(getter.Name, targetParam.Name, comparison)) is { } getter
                                ? (PropertyMapper<TSource, TArgumentState>?)getter.Accept(baseVisitor, state: targetParam)
                                : null;

                            if (mapper is null && targetParam.IsRequired)
                            {
                                ThrowCannotMapTypes(typeof(TSource), typeof(TTarget));
                            }

                            return mapper;
                        })
                        .Where(mapper => mapper != null)
                        .ToArray()!;

                    return new Mapper<TSource, TTarget>(source =>
                    {
                        if (source is null)
                        {
                            return default;
                        }

                        TArgumentState argumentState = argumentStateCtor();
                        foreach (PropertyMapper<TSource, TArgumentState> mapper in propertyMappers)
                        {
                            mapper(ref source, ref argumentState);
                        }

                        return ctor(ref argumentState);
                    });
                }
            }
        }

        private sealed class PropertyScopedVisitor<TSource, TSourceProperty>(Builder baseVisitor) : TypeShapeVisitor
        {
            public override object? VisitProperty<TTarget, TTargetProperty>(IPropertyShape<TTarget, TTargetProperty> targetProperty, object? state)
            {
                IPropertyShape<TSource, TSourceProperty> sourceProperty = (IPropertyShape<TSource, TSourceProperty>)state!;
                var propertyTypeMapper = baseVisitor.GetOrAddMapper(sourceProperty.PropertyType, targetProperty.PropertyType);

                Getter<TSource, TSourceProperty> sourceGetter = sourceProperty.GetGetter();
                Setter<TTarget, TTargetProperty> targetSetter = targetProperty.GetSetter();
                return new PropertyMapper<TSource, TTarget>((ref TSource source, ref TTarget target) =>
                {
                    TSourceProperty sourcePropertyValue = sourceGetter(ref source);
                    TTargetProperty targetPropertyValue = propertyTypeMapper(sourcePropertyValue)!;
                    targetSetter(ref target, targetPropertyValue);
                });
            }

            public override object? VisitParameter<TArgumentState, TTargetParameter>(IParameterShape<TArgumentState, TTargetParameter> targetParameter, object? state)
            {
                var sourceProperty = (IPropertyShape<TSource, TSourceProperty>)state!;
                var propertyTypeMapper = baseVisitor.GetOrAddMapper(sourceProperty.PropertyType, targetParameter.ParameterType);
                Getter<TSource, TSourceProperty> sourceGetter = sourceProperty.GetGetter();
                Setter<TArgumentState, TTargetParameter> parameterSetter = targetParameter.GetSetter();

                return new PropertyMapper<TSource, TArgumentState>((ref TSource source, ref TArgumentState target) =>
                {
                    TSourceProperty sourcePropertyValue = sourceGetter(ref source);
                    TTargetParameter targetParameterValue = propertyTypeMapper(sourcePropertyValue)!;
                    parameterSetter(ref target, targetParameterValue);
                });
            }
        }

        private sealed class NullableScopedVisitor<TOptional, TElement>(Builder baseVisitor) : TypeShapeVisitor
        {
            public override object? VisitOptional<TOptional2, TElement2>(IOptionalTypeShape<TOptional2, TElement2> optionalShape, object? state)
            {
                var sourceNullable = (IOptionalTypeShape<TOptional, TElement>)state!;
                var elementMapper = baseVisitor.GetOrAddMapper(sourceNullable.ElementType, optionalShape.ElementType);
                var deconstructor = sourceNullable.GetDeconstructor();
                var createNone = optionalShape.GetNoneConstructor();
                var createSome = optionalShape.GetSomeConstructor();
                return new Mapper<TOptional, TOptional2>(source => deconstructor(source, out TElement? element) ? createSome(elementMapper(element)!) : createNone());
            }
        }

        private sealed class SurrogateScopedVisitor<T1, TSurrogate1>(Builder baseVisitor) : TypeShapeVisitor
        {
            public override object? VisitSurrogate<T2, TSurrogate2>(ISurrogateTypeShape<T2, TSurrogate2> targetShape, object? state = null)
            {
                var sourceShape = (ISurrogateTypeShape<T1, TSurrogate1>)state!;
                var surrogateMapper = baseVisitor.GetOrAddMapper(sourceShape.SurrogateType, targetShape.SurrogateType);
                var leftMarshaler = sourceShape.Marshaler;
                var rightMarshaler = targetShape.Marshaler;
                return new Mapper<T1, T2>(source => rightMarshaler.Unmarshal(surrogateMapper(leftMarshaler.Marshal(source))));
            }
        }

        private sealed class EnumerableScopedVisitor<TSourceEnumerable, TSourceElement>(Builder baseVisitor) : TypeShapeVisitor
        {
            public override object? VisitEnumerable<TTargetEnumerable, TTargetElement>(IEnumerableTypeShape<TTargetEnumerable, TTargetElement> enumerableShape, object? state)
            {
                var sourceEnumerable = (IEnumerableTypeShape<TSourceEnumerable, TSourceElement>)state!;
                var sourceGetEnumerable = sourceEnumerable.GetGetPotentiallyBlockingEnumerable();

                var elementMapper = baseVisitor.GetOrAddMapper(sourceEnumerable.ElementType, enumerableShape.ElementType);

                switch (enumerableShape.ConstructionStrategy)
                {
                    case CollectionConstructionStrategy.Mutable:
                        var defaultCtor = enumerableShape.GetDefaultConstructor();
                        var appender = enumerableShape.GetAppender();
                        return new Mapper<TSourceEnumerable, TTargetEnumerable>(source =>
                        {
                            if (source is null)
                            {
                                return default;
                            }

                            var target = defaultCtor();
                            foreach (TSourceElement sourceElement in sourceGetEnumerable(source))
                            {
                                appender(ref target, elementMapper(sourceElement)!);
                            }

                            return target;
                        });

                    case CollectionConstructionStrategy.Parameterized:
                        var createSpan = enumerableShape.GetParameterizedConstructor();
                        return new Mapper<TSourceEnumerable, TTargetEnumerable>(source =>
                        {
                            if (source is null)
                            {
                                return default;
                            }

                            return createSpan(sourceGetEnumerable(source).Select(e => elementMapper(e!)).ToArray());
                        });

                    default:
                        ThrowCannotMapTypes(typeof(TSourceEnumerable), typeof(TTargetEnumerable));
                        return null;
                }
            }
        }

        private sealed class DictionaryScopedVisitor<TSourceDictionary, TSourceKey, TSourceValue>(Builder baseVisitor) : TypeShapeVisitor
            where TSourceKey : notnull
        {
            public override object? VisitDictionary<TTargetDictionary, TTargetKey, TTargetValue>(IDictionaryTypeShape<TTargetDictionary, TTargetKey, TTargetValue> targetDictionary, object? state)
            {
                var sourceDictionary = (IDictionaryTypeShape<TSourceDictionary, TSourceKey, TSourceValue>)state!;
                var sourceGetDictionary = sourceDictionary.GetGetDictionary();
                var keyMapper = baseVisitor.GetOrAddMapper(sourceDictionary.KeyType, targetDictionary.KeyType);
                var valueMapper = baseVisitor.GetOrAddMapper(sourceDictionary.ValueType, targetDictionary.ValueType);

                switch (targetDictionary.ConstructionStrategy)
                {
                    case CollectionConstructionStrategy.Mutable:
                        var defaultCtor = targetDictionary.GetDefaultConstructor();
                        var inserter = targetDictionary.GetInserter();
                        return new Mapper<TSourceDictionary, TTargetDictionary>(source =>
                        {
                            if (source is null)
                            {
                                return default;
                            }

                            var target = defaultCtor();
                            foreach (var sourceEntry in sourceGetDictionary(source))
                            {
                                inserter(ref target, keyMapper(sourceEntry.Key), valueMapper(sourceEntry.Value)!);
                            }

                            return target;
                        });

                    case CollectionConstructionStrategy.Parameterized:
                        var createFromSpan = targetDictionary.GetParameterizedConstructor();
                        return new Mapper<TSourceDictionary, TTargetDictionary>(source =>
                        {
                            if (source is null)
                            {
                                return default;
                            }

                            KeyValuePair<TTargetKey, TTargetValue>[] entries = sourceGetDictionary(source).Select(MapEntry).ToArray();
                            return createFromSpan(entries);
                        });

                    default:
                        ThrowCannotMapTypes(typeof(TSourceDictionary), typeof(TTargetDictionary));
                        return null;
                }

                KeyValuePair<TTargetKey, TTargetValue> MapEntry(KeyValuePair<TSourceKey, TSourceValue> entry)
                    => new(keyMapper(entry.Key), valueMapper(entry.Value)!);
            }
        }

        [DoesNotReturn]
        internal static void ThrowCannotMapTypes(Type left, Type right)
            => throw new InvalidOperationException($"Cannot map type '{left}' to '{right}'.");
    }

    // Defines a synthetic type shape representing pairs of types.
    private sealed class MapperShape<TSource, TTarget>(ITypeShape<TSource> source, ITypeShape<TTarget> target) : ITypeShape<Mapper<TSource, TTarget>>
    {
        public TypeShapeKind Kind => (TypeShapeKind)101;
        public ITypeShapeProvider Provider => source.Provider;
        public Type Type => typeof(Mapper<TSource, TTarget>);
        public ICustomAttributeProvider? AttributeProvider => typeof(Mapper<TSource, TTarget>);
        public IReadOnlyList<IMethodShape> Methods => [];
        public object? Accept(TypeShapeVisitor visitor, object? state = null) => ((MapperTypeShapeVisitor)visitor).VisitMapper(source, target, state);
        public object? Invoke(ITypeShapeFunc func, object? state = null) => func.Invoke(this, state);
        public Func<object>? GetAssociatedTypeFactory(Type relatedType) => throw new NotImplementedException();
        public ITypeShape? GetAssociatedTypeShape(Type associatedType) => throw new NotImplementedException();
    }

    private abstract class MapperTypeShapeVisitor : TypeShapeVisitor
    {
        public abstract object? VisitMapper<TSource, TTarget>(ITypeShape<TSource> source, ITypeShape<TTarget> target, object? state);
    }

    private sealed class DelayedMapperFactory : MapperTypeShapeVisitor, IDelayedValueFactory
    {
        public DelayedValue Create<T>(ITypeShape<T> typeShape) => (DelayedValue)typeShape.Accept(this)!;
        public override object? VisitMapper<TSource, TTarget>(ITypeShape<TSource> left, ITypeShape<TTarget> right, object? state)
        {
            return new DelayedValue<Mapper<TSource, TTarget>>(self => left => self.Result(left));
        }
    }
}
