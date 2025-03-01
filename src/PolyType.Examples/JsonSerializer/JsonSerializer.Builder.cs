using PolyType.Abstractions;
using PolyType.Examples.JsonSerializer.Converters;
using PolyType.Utilities;
using System.Diagnostics;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace PolyType.Examples.JsonSerializer;

public static partial class JsonSerializerTS
{
    private sealed class Builder(TypeGenerationContext generationContext) : ITypeShapeVisitor, ITypeShapeFunc
    {
        public JsonConverter<T> GetOrAddConverter<T>(ITypeShape<T> shape) =>
            (JsonConverter<T>)generationContext.GetOrAdd(shape)!;

        object? ITypeShapeFunc.Invoke<T>(ITypeShape<T> typeShape, object? state)
        {
            // Check if the type has a built-in converter.
            if (s_defaultConverters.TryGetValue(typeShape.Type, out JsonConverter? defaultConverter))
            {
                return defaultConverter;
            }

            // Otherwise, build a converter using the visitor.
            return typeShape.Accept(this);
        }

        public object? VisitObject<T>(IObjectTypeShape<T> type, object? state)
        {
            if (typeof(T) == typeof(object))
            {
                return new JsonPolymorphicObjectConverter(generationContext.ParentCache!);
            }

            JsonPropertyConverter<T>[] properties = type.Properties
                .Select(prop => (JsonPropertyConverter<T>)prop.Accept(this)!)
                .ToArray();

            return type.Constructor is { } ctor
                ? (JsonObjectConverter<T>)ctor.Accept(this, state: properties)!
                : new JsonObjectConverter<T>(properties);
        }

        public object? VisitProperty<TDeclaringType, TPropertyType>(IPropertyShape<TDeclaringType, TPropertyType> property, object? state)
        {
            JsonConverter<TPropertyType> propertyConverter = GetOrAddConverter(property.PropertyType);
            return new JsonPropertyConverter<TDeclaringType, TPropertyType>(property, propertyConverter);
        }

        public object? VisitConstructor<TDeclaringType, TArgumentState>(IConstructorShape<TDeclaringType, TArgumentState> constructor, object? state)
        {
            var properties = (JsonPropertyConverter<TDeclaringType>[])state!;

            if (constructor.Parameters is [])
            {
                return new JsonObjectConverterWithDefaultCtor<TDeclaringType>(constructor.GetDefaultConstructor(), properties);
            }

            JsonPropertyConverter<TArgumentState>[] constructorParams = constructor.Parameters
                .Select(param => (JsonPropertyConverter<TArgumentState>)param.Accept(this)!)
                .ToArray();

            return new JsonObjectConverterWithParameterizedCtor<TDeclaringType, TArgumentState>(
                constructor.GetArgumentStateConstructor(), 
                constructor.GetParameterizedConstructor(), 
                constructorParams,
                properties);
        }

        public object? VisitConstructorParameter<TArgumentState, TParameter>(IConstructorParameterShape<TArgumentState, TParameter> parameter, object? state)
        {
            JsonConverter<TParameter> paramConverter = GetOrAddConverter(parameter.ParameterType);
            return new JsonPropertyConverter<TArgumentState, TParameter>(parameter, paramConverter);
        }

        public object? VisitEnumerable<TEnumerable, TElement>(IEnumerableTypeShape<TEnumerable, TElement> enumerableShape, object? state)
        {
            JsonConverter<TElement> elementConverter = GetOrAddConverter(enumerableShape.ElementType);

            if (enumerableShape.Rank > 1)
            {
                Debug.Assert(typeof(TEnumerable).IsArray);
                return new JsonMDArrayConverter<TEnumerable, TElement>(elementConverter, enumerableShape.Rank);
            }

            return enumerableShape.ConstructionStrategy switch
            {
                CollectionConstructionStrategy.Mutable => 
                    new JsonMutableEnumerableConverter<TEnumerable, TElement>(
                        elementConverter,
                        enumerableShape,
                        enumerableShape.GetDefaultConstructor(),
                        enumerableShape.GetAddElement()),

                CollectionConstructionStrategy.Enumerable => 
                    new JsonEnumerableConstructorEnumerableConverter<TEnumerable, TElement>(
                        elementConverter,
                        enumerableShape,
                        enumerableShape.GetEnumerableConstructor()),

                CollectionConstructionStrategy.Span => 
                    new JsonSpanConstructorEnumerableConverter<TEnumerable, TElement>(
                        elementConverter,
                        enumerableShape,
                        enumerableShape.GetSpanConstructor()),
                _ => new JsonEnumerableConverter<TEnumerable, TElement>(elementConverter, enumerableShape),
            };
        }

        public object? VisitDictionary<TDictionary, TKey, TValue>(IDictionaryTypeShape<TDictionary, TKey, TValue> dictionaryShape, object? state) where TKey : notnull
        {
            JsonConverter<TKey> keyConverter = GetOrAddConverter(dictionaryShape.KeyType);
            JsonConverter<TValue> valueConverter = GetOrAddConverter(dictionaryShape.ValueType);

            return dictionaryShape.ConstructionStrategy switch
            {
                CollectionConstructionStrategy.Mutable => 
                    new JsonMutableDictionaryConverter<TDictionary, TKey, TValue>(
                        keyConverter,
                        valueConverter,
                        dictionaryShape,
                        dictionaryShape.GetDefaultConstructor(),
                        dictionaryShape.GetAddKeyValuePair()),

                CollectionConstructionStrategy.Enumerable => 
                    new JsonEnumerableConstructorDictionaryConverter<TDictionary, TKey, TValue>(
                        keyConverter,
                        valueConverter,
                        dictionaryShape,
                        dictionaryShape.GetEnumerableConstructor()),

                CollectionConstructionStrategy.Span => 
                    new JsonSpanConstructorDictionaryConverter<TDictionary, TKey, TValue>(
                        keyConverter,
                        valueConverter,
                        dictionaryShape,
                        dictionaryShape.GetSpanConstructor()),

                _ => new JsonDictionaryConverter<TDictionary, TKey, TValue>(keyConverter, valueConverter, dictionaryShape),
            };
        }

        public object? VisitOptional<TOptional, TElement>(IOptionalTypeShape<TOptional, TElement> optionalShape, object? state)
        {
            return new JsonOptionalConverter<TOptional, TElement>(
                elementConverter: GetOrAddConverter(optionalShape.ElementType),
                deconstructor: optionalShape.GetDeconstructor(),
                createNone: optionalShape.GetNoneConstructor(),
                createSome: optionalShape.GetSomeConstructor());
        }

        public object? VisitEnum<TEnum, TUnderlying>(IEnumTypeShape<TEnum, TUnderlying> enumShape, object? state) where TEnum : struct, Enum
        {
            var converter = new JsonStringEnumConverter<TEnum>();
            return converter.CreateConverter(typeof(TEnum), s_options);
        }

        public object? VisitSurrogate<T, TSurrogate>(ISurrogateTypeShape<T, TSurrogate> surrogateShape, object? state)
        {
            JsonConverter<TSurrogate> surrogateConverter = GetOrAddConverter(surrogateShape.SurrogateType);
            return new JsonSurrogateConverter<T, TSurrogate>(surrogateShape.Marshaller, surrogateConverter);
        }

        public object? VisitUnion<TUnion>(IUnionTypeShape<TUnion> unionShape, object? state)
        {
            var getUnionCaseIndex = unionShape.GetGetUnionCaseIndex();
            var baseTypeConverter = (JsonConverter<TUnion>)unionShape.BaseType.Invoke(this)!;
            var unionCases = unionShape.UnionCases
                .Select(unionCase => (JsonUnionCaseConverter<TUnion>)unionCase.Accept(this, null)!)
                .ToArray();

            return new JsonUnionConverter<TUnion>(getUnionCaseIndex, baseTypeConverter, unionCases);
        }

        public object? VisitUnionCase<TUnionCase, TUnion>(IUnionCaseShape<TUnionCase, TUnion> unionCaseShape, object? state) where TUnionCase : TUnion
        {
            var caseConverter = (JsonConverter<TUnionCase>)unionCaseShape.Type.Accept(this)!;
            return new JsonUnionCaseConverter<TUnionCase, TUnion>(unionCaseShape.Name, caseConverter);
        }

        private static readonly Dictionary<Type, JsonConverter> s_defaultConverters = new JsonConverter[]
        {
            JsonMetadataServices.BooleanConverter,
            JsonMetadataServices.SByteConverter,
            JsonMetadataServices.Int16Converter,
            JsonMetadataServices.Int32Converter,
            JsonMetadataServices.Int64Converter,
            JsonMetadataServices.ByteConverter,
            JsonMetadataServices.ByteArrayConverter,
            JsonMetadataServices.UInt16Converter,
            JsonMetadataServices.UInt32Converter,
            JsonMetadataServices.UInt64Converter,
            JsonMetadataServices.CharConverter,
            JsonMetadataServices.StringConverter,
            JsonMetadataServices.SingleConverter,
            JsonMetadataServices.DoubleConverter,
            JsonMetadataServices.DecimalConverter,
            JsonMetadataServices.DateTimeConverter,
            JsonMetadataServices.DateTimeOffsetConverter,
            JsonMetadataServices.TimeSpanConverter,
#if NET
            JsonMetadataServices.Int128Converter,
            JsonMetadataServices.UInt128Converter,
            JsonMetadataServices.HalfConverter,
            JsonMetadataServices.DateOnlyConverter,
            JsonMetadataServices.TimeOnlyConverter,
            new RuneConverter(),
#endif
            JsonMetadataServices.GuidConverter,
            JsonMetadataServices.UriConverter,
            JsonMetadataServices.VersionConverter,
            new BigIntegerConverter(),
        }.ToDictionary(conv => conv.Type!);
    }
}
