using PolyType.Abstractions;
using PolyType.Examples.Utilities;
using PolyType.Examples.YamlSerializer.Converters;
using PolyType.Utilities;

namespace PolyType.Examples.YamlSerializer;

public static partial class YamlSerializer
{
    private sealed class Builder(TypeGenerationContext self) : TypeShapeVisitor, ITypeShapeFunc
    {
        /// <summary>Recursively looks up or creates a converter for the specified shape.</summary>
        public YamlConverter<T> GetOrAddConverter<T>(ITypeShape<T> shape) =>
            (YamlConverter<T>)self.GetOrAdd(shape)!;

        object? ITypeShapeFunc.Invoke<T>(ITypeShape<T> typeShape, object? state)
        {
            if (s_defaultConverters.TryGetValue(typeof(T), out IYamlConverter? defaultConverter))
            {
                return (YamlConverter<T>)defaultConverter;
            }

            return typeShape.Accept(this);
        }

        public override object? VisitObject<T>(IObjectTypeShape<T> type, object? state)
        {
            if (typeof(T) == typeof(object))
            {
                return new ObjectConverter(self.ParentCache!);
            }

            YamlPropertyConverter<T>[] properties = type.Properties
                .Select(prop => (YamlPropertyConverter<T>)prop.Accept(this)!)
                .ToArray();

            return type.Constructor is { } ctor
                ? (YamlObjectConverter<T>)ctor.Accept(this, state: properties)!
                : new YamlObjectConverter<T>(properties);
        }

        public override object? VisitProperty<TDeclaringType, TPropertyType>(IPropertyShape<TDeclaringType, TPropertyType> property, object? state)
        {
            YamlConverter<TPropertyType> propertyConverter = GetOrAddConverter(property.PropertyType);

            return new YamlPropertyConverter<TDeclaringType, TPropertyType>(property, propertyConverter);
        }

        public override object? VisitConstructor<TDeclaringType, TArgumentState>(IConstructorShape<TDeclaringType, TArgumentState> constructor, object? state)
        {
            var properties = (YamlPropertyConverter<TDeclaringType>[])state!;

            if (constructor.Parameters is [])
            {
                return new YamlObjectConverterWithDefaultCtor<TDeclaringType>(constructor.GetDefaultConstructor(), properties);
            }

            YamlPropertyConverter<TArgumentState>[] constructorParams = constructor.Parameters
                .Select(param => (YamlPropertyConverter<TArgumentState>)param.Accept(this)!)
                .ToArray();

            return new YamlObjectConverterWithParameterizedCtor<TDeclaringType, TArgumentState>(
                constructor.GetArgumentStateConstructor(),
                constructor.GetParameterizedConstructor(),
                constructorParams,
                properties,
                constructor.Parameters);
        }

        public override object? VisitParameter<TArgumentState, TParameterType>(IParameterShape<TArgumentState, TParameterType> parameter, object? state)
        {
            YamlConverter<TParameterType> paramConverter = GetOrAddConverter(parameter.ParameterType);

            return new YamlPropertyConverter<TArgumentState, TParameterType>(parameter, paramConverter);
        }

        public override object? VisitEnumerable<TEnumerable, TElement>(IEnumerableTypeShape<TEnumerable, TElement> enumerableShape, object? state)
        {
            YamlConverter<TElement> elementConverter = GetOrAddConverter(enumerableShape.ElementType);
            Func<TEnumerable, IEnumerable<TElement>> getEnumerable = enumerableShape.GetGetPotentiallyBlockingEnumerable();

            return enumerableShape.ConstructionStrategy switch
            {
                CollectionConstructionStrategy.Mutable =>
                    new YamlMutableEnumerableConverter<TEnumerable, TElement>(
                        elementConverter,
                        getEnumerable,
                        enumerableShape.GetDefaultConstructor(),
                        enumerableShape.GetAppender()),
                CollectionConstructionStrategy.Parameterized =>
                    new YamlParameterizedEnumerableConverter<TEnumerable, TElement>(
                        elementConverter,
                        getEnumerable,
                        enumerableShape.GetParameterizedConstructor()),
                _ => new YamlEnumerableConverter<TEnumerable, TElement>(elementConverter, getEnumerable),
            };
        }

        public override object? VisitDictionary<TDictionary, TKey, TValue>(IDictionaryTypeShape<TDictionary, TKey, TValue> dictionaryShape, object? state)
        {
            YamlConverter<TKey> keyConverter = GetOrAddConverter(dictionaryShape.KeyType);
            YamlConverter<TValue> valueConverter = GetOrAddConverter(dictionaryShape.ValueType);
            Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> getEnumerable = dictionaryShape.GetGetDictionary();

            return dictionaryShape.ConstructionStrategy switch
            {
                CollectionConstructionStrategy.Mutable =>
                    new YamlMutableDictionaryConverter<TDictionary, TKey, TValue>(
                        keyConverter,
                        valueConverter,
                        getEnumerable,
                        dictionaryShape.GetDefaultConstructor(),
                        dictionaryShape.GetInserter(DictionaryInsertionMode.Throw)),

                CollectionConstructionStrategy.Parameterized =>
                    new YamlParameterizedDictionaryConverter<TDictionary, TKey, TValue>(
                        keyConverter,
                        valueConverter,
                        getEnumerable,
                        dictionaryShape.GetParameterizedConstructor()),

                _ => new YamlDictionaryConverter<TDictionary, TKey, TValue>(keyConverter, valueConverter, getEnumerable),
            };
        }

        public override object? VisitOptional<TOptional, TElement>(IOptionalTypeShape<TOptional, TElement> optionalShape, object? state)
        {
            return new YamlOptionalConverter<TOptional, TElement>(
                elementConverter: GetOrAddConverter(optionalShape.ElementType),
                deconstructor: optionalShape.GetDeconstructor(),
                createNone: optionalShape.GetNoneConstructor(),
                createSome: optionalShape.GetSomeConstructor());
        }

        public override object? VisitEnum<TEnum, TUnderlying>(IEnumTypeShape<TEnum, TUnderlying> enumShape, object? state)
        {
            return new YamlEnumConverter<TEnum>();
        }

        public override object? VisitSurrogate<T, TSurrogate>(ISurrogateTypeShape<T, TSurrogate> surrogateShape, object? state)
        {
            YamlConverter<TSurrogate> surrogateConverter = GetOrAddConverter(surrogateShape.SurrogateType);

            return new YamlSurrogateConverter<T, TSurrogate>(surrogateShape.Marshaler, surrogateConverter);
        }

        public override object? VisitUnion<TUnion>(IUnionTypeShape<TUnion> unionShape, object? state)
        {
            var getUnionCaseIndex = unionShape.GetGetUnionCaseIndex();
            var baseCaseConverter = (YamlConverter<TUnion>)unionShape.BaseType.Accept(this)!;
            var unionCaseConverters = unionShape.UnionCases
                .Select(unionCase =>
                {
                    var caseConverter = (YamlConverter<TUnion>)unionCase.Accept(this)!;

                    return new KeyValuePair<string, YamlConverter<TUnion>>(unionCase.Name, caseConverter);
                })
                .ToArray();

            return new YamlUnionConverter<TUnion>(getUnionCaseIndex, baseCaseConverter, unionCaseConverters);
        }

        public override object? VisitUnionCase<TUnionCase, TUnion>(IUnionCaseShape<TUnionCase, TUnion> unionCaseShape, object? state)
        {
            var caseConverter = (YamlConverter<TUnionCase>)unionCaseShape.UnionCaseType.Invoke(this)!;

            return new YamlUnionCaseConverter<TUnionCase, TUnion>(caseConverter, unionCaseShape.Marshaler);
        }

        public override object? VisitFunction<TFunction, TArgumentState, TResult>(IFunctionTypeShape<TFunction, TArgumentState, TResult> functionShape, object? state = null)
        {
            return new YamlObjectConverter<TFunction>([]);
        }

        private static readonly Dictionary<Type, IYamlConverter> s_defaultConverters = new IYamlConverter[]
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
