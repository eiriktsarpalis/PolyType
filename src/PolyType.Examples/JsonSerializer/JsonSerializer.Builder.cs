using PolyType.Abstractions;
using PolyType.Examples.JsonSerializer.Converters;
using PolyType.Utilities;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace PolyType.Examples.JsonSerializer;

public static partial class JsonSerializerTS
{
    private sealed class Builder(TypeGenerationContext generationContext) : TypeShapeVisitor, ITypeShapeFunc
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

        public override object? VisitObject<T>(IObjectTypeShape<T> type, object? state)
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

        public override object? VisitProperty<TDeclaringType, TPropertyType>(IPropertyShape<TDeclaringType, TPropertyType> property, object? state)
        {
            JsonConverter<TPropertyType> propertyConverter = GetOrAddConverter(property.PropertyType);
            return new JsonPropertyConverter<TDeclaringType, TPropertyType>(property, propertyConverter);
        }

        public override object? VisitConstructor<TDeclaringType, TArgumentState>(IConstructorShape<TDeclaringType, TArgumentState> constructor, object? state)
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
                properties,
                constructor.Parameters);
        }

        public override object? VisitParameter<TArgumentState, TParameter>(IParameterShape<TArgumentState, TParameter> parameter, object? state)
        {
            if (state is IMethodShape)
            {
                if (parameter.ParameterType.Type == typeof(CancellationToken))
                {
                    var tokenSetter = (Setter<TArgumentState, CancellationToken>)(object)parameter.GetSetter();
                    return new MethodParameterSetter<TArgumentState>((ref TArgumentState state, IReadOnlyDictionary<string, JsonElement> _, CancellationToken token) =>
                    {
                        tokenSetter(ref state, token);
                    });
                }

                JsonConverter<TParameter> paramConverter = GetOrAddConverter(parameter.ParameterType);
                var setter = parameter.GetSetter();
                return new MethodParameterSetter<TArgumentState>((ref TArgumentState state, IReadOnlyDictionary<string, JsonElement> parameters, CancellationToken cancellationToken) =>
                {
                    if (parameters.TryGetValue(parameter.Name, out JsonElement value))
                    {
                        TParameter? parameter = paramConverter.Deserialize(value);
                        setter(ref state, parameter!);
                    }
                });
            }

            return new JsonPropertyConverter<TArgumentState, TParameter>(parameter, GetOrAddConverter(parameter.ParameterType));
        }

        public override object? VisitEnumerable<TEnumerable, TElement>(IEnumerableTypeShape<TEnumerable, TElement> enumerableShape, object? state)
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
                        enumerableShape.GetAppender()),

                CollectionConstructionStrategy.Parameterized => 
                    new JsonParameterizedEnumerableConverter<TEnumerable, TElement>(
                        elementConverter,
                        enumerableShape,
                        enumerableShape.GetParameterizedConstructor()),
                _ => new JsonEnumerableConverter<TEnumerable, TElement>(elementConverter, enumerableShape),
            };
        }

        public override object? VisitDictionary<TDictionary, TKey, TValue>(IDictionaryTypeShape<TDictionary, TKey, TValue> dictionaryShape, object? state)
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
                        dictionaryShape.GetInserter(DictionaryInsertionMode.Overwrite)),

                CollectionConstructionStrategy.Parameterized => 
                    new JsonParameterizedDictionaryConverter<TDictionary, TKey, TValue>(
                        keyConverter,
                        valueConverter,
                        dictionaryShape,
                        dictionaryShape.GetParameterizedConstructor()),

                _ => new JsonDictionaryConverter<TDictionary, TKey, TValue>(keyConverter, valueConverter, dictionaryShape),
            };
        }

        public override object? VisitOptional<TOptional, TElement>(IOptionalTypeShape<TOptional, TElement> optionalShape, object? state)
        {
            return new JsonOptionalConverter<TOptional, TElement>(
                elementConverter: GetOrAddConverter(optionalShape.ElementType),
                deconstructor: optionalShape.GetDeconstructor(),
                createNone: optionalShape.GetNoneConstructor(),
                createSome: optionalShape.GetSomeConstructor());
        }

        public override object? VisitEnum<TEnum, TUnderlying>(IEnumTypeShape<TEnum, TUnderlying> enumShape, object? state)
        {
            var converter = new JsonStringEnumConverter<TEnum>();
            return converter.CreateConverter(typeof(TEnum), s_options);
        }

        public override object? VisitSurrogate<T, TSurrogate>(ISurrogateTypeShape<T, TSurrogate> surrogateShape, object? state)
        {
            JsonConverter<TSurrogate> surrogateConverter = GetOrAddConverter(surrogateShape.SurrogateType);
            return new JsonSurrogateConverter<T, TSurrogate>(surrogateShape.Marshaller, surrogateConverter);
        }

        public override object? VisitUnion<TUnion>(IUnionTypeShape<TUnion> unionShape, object? state)
        {
            var getUnionCaseIndex = unionShape.GetGetUnionCaseIndex();
            var baseTypeConverter = (JsonConverter<TUnion>)unionShape.BaseType.Invoke(this)!;
            var unionCases = unionShape.UnionCases
                .Select(unionCase => (JsonUnionCaseConverter<TUnion>)unionCase.Accept(this, null)!)
                .ToArray();

            return new JsonUnionConverter<TUnion>(getUnionCaseIndex, baseTypeConverter, unionCases);
        }

        public override object? VisitUnionCase<TUnionCase, TUnion>(IUnionCaseShape<TUnionCase, TUnion> unionCaseShape, object? state)
        {
            var caseConverter = (JsonConverter<TUnionCase>)unionCaseShape.Type.Accept(this)!;
            return new JsonUnionCaseConverter<TUnionCase, TUnion>(unionCaseShape.Name, caseConverter);
        }

        public override object? VisitMethod<TDeclaringType, TArgumentState, TResult>(IMethodShape<TDeclaringType, TArgumentState, TResult> methodShape, object? state = null)
        {
            // Store the target instance as a boxed value to ensure appropriate handling of struct methods.
            StrongBox<TDeclaringType?> boxedTarget;
            if (methodShape.IsStatic)
            {
                boxedTarget = new(default);
            }
            else
            {
                if (state is not TDeclaringType instance)
                {
                    throw new InvalidOperationException($"Expected a target of type {typeof(TDeclaringType).FullName}, but got {state?.GetType().FullName ?? "null"}.");
                }

                boxedTarget = new(instance);
            }

            var argumentStateCtor = methodShape.GetArgumentStateConstructor();
            var invoker = methodShape.GetMethodInvoker();
            var resultConverter = GetOrAddConverter(methodShape.ReturnType);
            var parameterSetters = methodShape.Parameters
                .Select(p => (MethodParameterSetter<TArgumentState>)p.Accept(this, methodShape)!)
                .ToArray();

            return new JsonFunc(async (parameters, cancellationToken) =>
            {
                TArgumentState argumentState = argumentStateCtor();

                foreach (var setter in parameterSetters)
                {
                    setter(ref argumentState, parameters, cancellationToken);
                }

                if (!argumentState.AreRequiredArgumentsSet)
                {
                    ThrowMissingRequiredArguments(ref argumentState);
                }

                TResult result = await invoker(ref boxedTarget.Value, ref argumentState).ConfigureAwait(false);
                return resultConverter.SerializeToElement(result);
            });

            void ThrowMissingRequiredArguments(ref TArgumentState argumentState)
            {
                List<string>? missingParameters = [];
                foreach (var parameter in methodShape.Parameters)
                {
                    if (parameter.IsRequired && !argumentState.IsArgumentSet(parameter.Position))
                    {
                        missingParameters.Add(parameter.Name);
                    }
                }

                throw new JsonException($"Method invocation is missing required parameters: {string.Join(", ", missingParameters)}");
            }
        }

        private delegate void MethodParameterSetter<TArgumentState>(
            ref TArgumentState argumentState,
            IReadOnlyDictionary<string, JsonElement> parameters,
            CancellationToken cancellationToken);

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
