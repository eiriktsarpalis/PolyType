using PolyType.Abstractions;
using PolyType.Examples.Utilities;
using PolyType.Examples.XmlSerializer.Converters;
using PolyType.Utilities;

namespace PolyType.Examples.XmlSerializer;

public static partial class XmlSerializer
{
    private sealed class Builder(TypeGenerationContext self) : TypeShapeVisitor, ITypeShapeFunc
    {
        /// <summary>Recursively looks up or creates a converter for the specified shape.</summary>
        public XmlConverter<T> GetOrAddConverter<T>(ITypeShape<T> shape) =>
            (XmlConverter<T>)self.GetOrAdd(shape)!;

        object? ITypeShapeFunc.Invoke<T>(ITypeShape<T> typeShape, object? state)
        {
            if (s_defaultConverters.TryGetValue(typeof(T), out IXmlConverter? defaultConverter))
            {
                return (XmlConverter<T>)defaultConverter;
            }

            return typeShape.Accept(this);
        }

        public override object? VisitObject<T>(IObjectTypeShape<T> type, object? state)
        {
            if (typeof(T) == typeof(object))
            {
                return new ObjectConverter(self.ParentCache!);
            }

            XmlPropertyConverter<T>[] properties = type.Properties
                .Select(prop => (XmlPropertyConverter<T>)prop.Accept(this)!)
                .ToArray();

            // Prefer the default constructor if available.
            return type.Constructor is { } ctor
                ? (XmlObjectConverter<T>)ctor.Accept(this, state: properties)!
                : new XmlObjectConverter<T>(properties);
        }

        public override object? VisitProperty<TDeclaringType, TPropertyType>(IPropertyShape<TDeclaringType, TPropertyType> property, object? state)
        {
            XmlConverter<TPropertyType> propertyConverter = GetOrAddConverter(property.PropertyType);
            return new XmlPropertyConverter<TDeclaringType, TPropertyType>(property, propertyConverter);
        }

        public override object? VisitConstructor<TDeclaringType, TArgumentState>(IConstructorShape<TDeclaringType, TArgumentState> constructor, object? state)
        {
            var properties = (XmlPropertyConverter<TDeclaringType>[])state!;

            if (constructor.Parameters is [])
            {
                return new XmlObjectConverterWithDefaultCtor<TDeclaringType>(constructor.GetDefaultConstructor(), properties);
            }

            XmlPropertyConverter<TArgumentState>[] constructorParams = constructor.Parameters
                .Select(param => (XmlPropertyConverter<TArgumentState>)param.Accept(this)!)
                .ToArray();

            return new XmlObjectConverterWithParameterizedCtor<TDeclaringType, TArgumentState>(
                constructor.GetArgumentStateConstructor(),
                constructor.GetParameterizedConstructor(),
                constructorParams,
                properties);
        }

        public override object? VisitParameter<TArgumentState, TParameterType>(IParameterShape<TArgumentState, TParameterType> parameter, object? state)
        {
            XmlConverter<TParameterType> paramConverter = GetOrAddConverter(parameter.ParameterType);
            return new XmlPropertyConverter<TArgumentState, TParameterType>(parameter, paramConverter);
        }

        public override object? VisitEnumerable<TEnumerable, TElement>(IEnumerableTypeShape<TEnumerable, TElement> enumerableShape, object? state)
        {
            XmlConverter<TElement> elementConverter = GetOrAddConverter(enumerableShape.ElementType);
            Func<TEnumerable, IEnumerable<TElement>> getEnumerable = enumerableShape.GetGetPotentiallyBlockingEnumerable();

            return enumerableShape.ConstructionStrategy switch
            {
                CollectionConstructionStrategy.Mutable => 
                    new XmlMutableEnumerableConverter<TEnumerable, TElement>(
                        elementConverter,
                        getEnumerable,
                        enumerableShape.GetDefaultConstructor(),
                        enumerableShape.GetAddElement()),
                CollectionConstructionStrategy.Enumerable => 
                    new XmlEnumerableConstructorEnumerableConverter<TEnumerable, TElement>(
                        elementConverter,
                        getEnumerable,
                        enumerableShape.GetEnumerableConstructor()),
                CollectionConstructionStrategy.Span => 
                    new XmlSpanConstructorEnumerableConverter<TEnumerable, TElement>(
                        elementConverter,
                        getEnumerable,
                        enumerableShape.GetSpanConstructor()),
                _ => new XmlEnumerableConverter<TEnumerable, TElement>(elementConverter, getEnumerable),
            };
        }

        public override object? VisitDictionary<TDictionary, TKey, TValue>(IDictionaryTypeShape<TDictionary, TKey, TValue> dictionaryShape, object? state)
        {
            XmlConverter<TKey> keyConverter = GetOrAddConverter(dictionaryShape.KeyType);
            XmlConverter<TValue> valueConverter = GetOrAddConverter(dictionaryShape.ValueType);
            Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> getEnumerable = dictionaryShape.GetGetDictionary();

            return dictionaryShape.ConstructionStrategy switch
            {
                CollectionConstructionStrategy.Mutable => 
                    new XmlMutableDictionaryConverter<TDictionary, TKey, TValue>(
                        keyConverter,
                        valueConverter,
                        getEnumerable,
                        dictionaryShape.GetDefaultConstructor(),
                        dictionaryShape.GetAddKeyValuePair()),

                CollectionConstructionStrategy.Enumerable => 
                    new XmlEnumerableConstructorDictionaryConverter<TDictionary, TKey, TValue>(
                        keyConverter,
                        valueConverter,
                        getEnumerable,
                        dictionaryShape.GetEnumerableConstructor()),

                CollectionConstructionStrategy.Span => 
                    new XmlSpanConstructorDictionaryConverter<TDictionary, TKey, TValue>(
                        keyConverter,
                        valueConverter,
                        getEnumerable,
                        dictionaryShape.GetSpanConstructor()),

                _ => new XmlDictionaryConverter<TDictionary, TKey, TValue>(keyConverter, valueConverter, getEnumerable),
            };
        }

        public override object? VisitOptional<TOptional, TElement>(IOptionalTypeShape<TOptional, TElement> optionalShape, object? state)
        {
            return new XmlOptionalConverter<TOptional, TElement>(
                elementConverter: GetOrAddConverter(optionalShape.ElementType),
                deconstructor: optionalShape.GetDeconstructor(),
                createNone: optionalShape.GetNoneConstructor(),
                createSome: optionalShape.GetSomeConstructor());
        }

        public override object? VisitEnum<TEnum, TUnderlying>(IEnumTypeShape<TEnum, TUnderlying> enumShape, object? state)
        {
            return new XmlEnumConverter<TEnum>();
        }

        public override object? VisitSurrogate<T, TSurrogate>(ISurrogateTypeShape<T, TSurrogate> surrogateShape, object? state)
        {
            XmlConverter<TSurrogate> surrogateConverter = GetOrAddConverter(surrogateShape.SurrogateType);
            return new XmlSurrogateConverter<T, TSurrogate>(surrogateShape.Marshaller, surrogateConverter);
        }

        public override object? VisitUnion<TUnion>(IUnionTypeShape<TUnion> unionShape, object? state)
        {
            var getUnionCaseIndex = unionShape.GetGetUnionCaseIndex();
            var baseCaseConverter = (XmlConverter<TUnion>)unionShape.BaseType.Accept(this)!;
            var unionCaseConverter = unionShape.UnionCases
                .Select(unionCase =>
                {
                    var caseConverter = (XmlConverter<TUnion>)unionCase.Accept(this)!;
                    return new KeyValuePair<string, XmlConverter<TUnion>>(unionCase.Name, caseConverter);
                })
                .ToArray();

            return new XmlUnionConverter<TUnion>(getUnionCaseIndex, baseCaseConverter, unionCaseConverter);
        }

        public override object? VisitUnionCase<TUnionCase, TUnion>(IUnionCaseShape<TUnionCase, TUnion> unionCaseShape, object? state)
        {
            // NB: don't use the cached converter for TUnionCase, as it might equal TUnion.
            var caseConverter = (XmlConverter<TUnionCase>)unionCaseShape.Type.Invoke(this)!;
            return new XmlUnionCaseConverter<TUnionCase, TUnion>(caseConverter);
        }

        private static readonly Dictionary<Type, IXmlConverter> s_defaultConverters = new IXmlConverter[]
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
            new BigIntegerConverter(),
            new UriConverter(),
            new VersionConverter(),
#if NET
            new Int128Converter(),
            new UInt128Converter(),
            new HalfConverter(),
            new DateOnlyConverter(),
            new TimeOnlyConverter(),
            new RuneConverter(),
#endif
        }.ToDictionary(conv => conv.Type);
    }
}
