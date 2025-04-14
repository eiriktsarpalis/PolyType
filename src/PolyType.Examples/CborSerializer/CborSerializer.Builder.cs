using PolyType.Abstractions;
using PolyType.Examples.CborSerializer.Converters;

namespace PolyType.Examples.CborSerializer;

public static partial class CborSerializer
{
    private sealed class Builder(ITypeShapeFunc self) : TypeShapeVisitor, ITypeShapeFunc
    {
        /// <summary>Recursively looks up or creates a converter for the specified shape.</summary>
        public CborConverter<T> GetOrAddConverter<T>(ITypeShape<T> typeShape) =>
            (CborConverter<T>)self.Invoke(typeShape, this)!;

        object? ITypeShapeFunc.Invoke<T>(ITypeShape<T> typeShape, object? shape)
        {
            // Check if the type has a built-in converter.
            if (s_builtInConverters.TryGetValue(typeof(T), out CborConverter? defaultConverter))
            {
                return (CborConverter<T>)defaultConverter;
            }

            // Look for a custom converter attribute.
            CborConverterAttribute? converterAttribute = (CborConverterAttribute?)typeof(T).GetCustomAttributes(typeof(CborConverterAttribute), false).FirstOrDefault();
            if (converterAttribute is not null)
            {
                Type converterType = converterAttribute.ConverterType;
                Func<object>? factory =
                    (typeShape.GetAssociatedTypeShape(converterType) as IObjectTypeShape)?.GetDefaultConstructor()
                    ?? throw new InvalidOperationException($"The type {typeof(T)} is missing its associated shape or default constructor for converter {converterType}.");

                var converter = (CborConverter<T>)factory();
                converter.TypeShape = typeShape;
                return converter;
            }

            // Otherwise, build a converter using the visitor.
            return typeShape.Accept(this);
        }

        public override object? VisitObject<T>(IObjectTypeShape<T> objectShape, object? state)
        {
            CborPropertyConverter<T>[] properties = objectShape.Properties
                .Select(prop => (CborPropertyConverter<T>)prop.Accept(this)!)
                .ToArray();

            return objectShape.Constructor is { } ctor
                ? (CborObjectConverter<T>)ctor.Accept(this, state: properties)!
                : new CborObjectConverter<T>(properties);
        }

        public override object? VisitProperty<TDeclaringType, TPropertyType>(IPropertyShape<TDeclaringType, TPropertyType> property, object? state)
        {
            CborConverter<TPropertyType> propertyConverter = GetOrAddConverter(property.PropertyType);
            return new CborPropertyConverter<TDeclaringType, TPropertyType>(property, propertyConverter);
        }

        public override object? VisitConstructor<TDeclaringType, TArgumentState>(IConstructorShape<TDeclaringType, TArgumentState> constructor, object? state)
        {
            var properties = (CborPropertyConverter<TDeclaringType>[])state!;

            if (constructor.Parameters is [])
            {
                return new CborObjectConverterWithDefaultCtor<TDeclaringType>(constructor.GetDefaultConstructor(), properties);
            }

            CborPropertyConverter<TArgumentState>[] constructorParams = constructor.Parameters
                .Select(param => (CborPropertyConverter<TArgumentState>)param.Accept(this)!)
                .ToArray();

            return new CborObjectConverterWithParameterizedCtor<TDeclaringType, TArgumentState>(
                constructor.GetArgumentStateConstructor(),
                constructor.GetParameterizedConstructor(),
                constructorParams,
                properties);
        }

        public override object? VisitParameter<TArgumentState, TParameterType>(IParameterShape<TArgumentState, TParameterType> parameter, object? state)
        {
            CborConverter<TParameterType> paramConverter = GetOrAddConverter(parameter.ParameterType);
            return new CborPropertyConverter<TArgumentState, TParameterType>(parameter, paramConverter);
        }

        public override object? VisitEnumerable<TEnumerable, TElement>(IEnumerableTypeShape<TEnumerable, TElement> enumerableShape, object? state)
        {
            CborConverter<TElement> elementConverter = GetOrAddConverter(enumerableShape.ElementType);
            Func<TEnumerable, IEnumerable<TElement>> getEnumerable = enumerableShape.GetGetEnumerable();

            return enumerableShape.ConstructionStrategy switch
            {
                CollectionConstructionStrategy.Mutable =>
                    new CborMutableEnumerableConverter<TEnumerable, TElement>(
                        elementConverter,
                        getEnumerable,
                        enumerableShape.GetDefaultConstructor(),
                        enumerableShape.GetAddElement()),

                CollectionConstructionStrategy.Enumerable =>
                    new CborEnumerableConstructorEnumerableConverter<TEnumerable, TElement>(
                        elementConverter,
                        getEnumerable,
                        enumerableShape.GetEnumerableConstructor()),

                CollectionConstructionStrategy.Span =>
                    new CborSpanConstructorEnumerableConverter<TEnumerable, TElement>(
                        elementConverter,
                        getEnumerable,
                        enumerableShape.GetSpanConstructor()),

                _ => new CborEnumerableConverter<TEnumerable, TElement>(elementConverter, getEnumerable),
            };
        }

        public override object? VisitDictionary<TDictionary, TKey, TValue>(IDictionaryTypeShape<TDictionary, TKey, TValue> dictionaryShape, object? state)
        {
            CborConverter<TKey> keyConverter = GetOrAddConverter(dictionaryShape.KeyType);
            CborConverter<TValue> valueConverter = GetOrAddConverter(dictionaryShape.ValueType);
            Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> getDictionary = dictionaryShape.GetGetDictionary();

            return dictionaryShape.ConstructionStrategy switch
            {
                CollectionConstructionStrategy.Mutable =>
                    new CborMutableDictionaryConverter<TDictionary, TKey, TValue>(
                        keyConverter,
                        valueConverter,
                        getDictionary,
                        dictionaryShape.GetDefaultConstructor(),
                        dictionaryShape.GetAddKeyValuePair()),

                CollectionConstructionStrategy.Enumerable =>
                    new CborEnumerableConstructorDictionaryConverter<TDictionary, TKey, TValue>(
                        keyConverter,
                        valueConverter,
                        getDictionary,
                        dictionaryShape.GetEnumerableConstructor()),

                CollectionConstructionStrategy.Span =>
                    new CborSpanConstructorDictionaryConverter<TDictionary, TKey, TValue>(
                        keyConverter,
                        valueConverter,
                        getDictionary,
                        dictionaryShape.GetSpanConstructor()),

                _ => new CborDictionaryConverter<TDictionary, TKey, TValue>(keyConverter, valueConverter, getDictionary),
            };
        }

        public override object? VisitOptional<TOptional, TElement>(IOptionalTypeShape<TOptional, TElement> optionalShape, object? state)
        {
            return new CborOptionalConverter<TOptional, TElement>(
                elementConverter: GetOrAddConverter(optionalShape.ElementType),
                deconstructor: optionalShape.GetDeconstructor(),
                createNone: optionalShape.GetNoneConstructor(),
                createSome: optionalShape.GetSomeConstructor());
        }

        public override object? VisitEnum<TEnum, TUnderlying>(IEnumTypeShape<TEnum, TUnderlying> enumShape, object? state)
        {
            return new CborEnumConverter<TEnum>();
        }

        public override object? VisitSurrogate<T, TSurrogate>(ISurrogateTypeShape<T, TSurrogate> surrogateShape, object? state)
        {
            CborConverter<TSurrogate> surrogateConverter = GetOrAddConverter(surrogateShape.SurrogateType);
            return new CborSurrogateConverter<T, TSurrogate>(surrogateShape.Marshaller, surrogateConverter);
        }

        public override object? VisitUnion<TUnion>(IUnionTypeShape<TUnion> unionShape, object? state)
        {
            var getUnionCaseIndex = unionShape.GetGetUnionCaseIndex();
            var baseTypeConverter = (CborConverter<TUnion>)unionShape.BaseType.Invoke(this)!;
            var unionCases = unionShape.UnionCases
                .Select(unionCase =>
                {
                    var caseConverter = (CborConverter<TUnion>)unionCase.Accept(this, null)!;
                    return new KeyValuePair<int, CborConverter<TUnion>>(unionCase.Tag, caseConverter);
                })
                .ToArray();

            return new CborUnionConverter<TUnion>(getUnionCaseIndex, baseTypeConverter, unionCases);
        }

        public override object? VisitUnionCase<TUnionCase, TUnion>(IUnionCaseShape<TUnionCase, TUnion> unionCaseShape, object? state)
        {
            // NB: don't use the cached converter for TUnionCase, as it might equal TUnion.
            var caseConverter = (CborConverter<TUnionCase>)unionCaseShape.Type.Invoke(this)!;
            return new CborUnionCaseConverter<TUnionCase, TUnion>(caseConverter);
        }

        private static readonly Dictionary<Type, CborConverter> s_builtInConverters = new CborConverter[]
        {
            new BoolConverter(),
            new StringConverter(),
            new SByteConverter(),
            new Int16Converter(),
            new Int32Converter(),
            new Int64Converter(),
            new ByteConverter(),
            new ByteArrayConverter(),
            new UInt16Converter(),
            new UInt32Converter(),
            new UInt64Converter(),
            new CharConverter(),
            new SingleConverter(),
            new DoubleConverter(),
            new DecimalConverter(),
            new DateTimeConverter(),
            new DateTimeOffsetConverter(),
            new TimeSpanConverter(),
            new GuidConverter(),
            new UriConverter(),
            new VersionConverter(),
            new BigIntegerConverter(),
#if NET
            new Int128Converter(),
            new UInt128Converter(),
            new HalfConverter(),
            new DateOnlyConverter(),
            new TimeOnlyConverter(),
            new RuneConverter(),
#endif
            new ObjectConverter(),
        }.ToDictionary(conv => conv.Type);
    }
}
