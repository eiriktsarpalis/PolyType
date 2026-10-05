using PolyType.Abstractions;
using PolyType.Examples.JsonSerializer.Converters;
using PolyType.Utilities;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace PolyType.Examples.JsonSerializer;

public static partial class JsonSerializerTS
{
    internal static JsonValueType GetJsonValueType(JsonConverter converter) => Builder.GetJsonValueType(converter);

    private sealed class Builder(TypeGenerationContext generationContext) : TypeShapeVisitor, ITypeShapeFunc
    {
        public JsonConverter<T> GetOrAddConverter<T>(ITypeShape<T> shape) =>
            (JsonConverter<T>)generationContext.GetOrAdd(shape)!;

        object? ITypeShapeFunc.Invoke<T>(ITypeShape<T> typeShape, object? state)
        {
            // Check if the type has a built-in converter.
            if (s_defaultConverters.TryGetValue(typeShape.Type, out var builtIn))
            {
                return builtIn.Converter;
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
                    return new MethodParameterSetter<TArgumentState>((ref state, _, token) =>
                    {
                        tokenSetter(ref state, token);
                    });
                }

                JsonConverter<TParameter> paramConverter = GetOrAddConverter(parameter.ParameterType);
                var setter = parameter.GetSetter();
                return new MethodParameterSetter<TArgumentState>((ref state, parameters, cancellationToken) =>
                {
                    if (parameters.TryGetValue(parameter.Name, out JsonElement value))
                    {
                        TParameter? parameter = paramConverter.Deserialize(value);
                        setter(ref state, parameter!);
                    }
                });
            }

            if (state is IFunctionTypeShape)
            {
                if (parameter.ParameterType.Type == typeof(object) && parameter.Name == "sender")
                {
                    var senderGetter = (Getter<TArgumentState, object?>)(object)parameter.GetGetter();
                    return new FunctionParameterGetter<TArgumentState>((
                        ref state,
                        _,
                        ref sender,
                        ref _) =>
                    {
                        sender = senderGetter(ref state);
                    });
                }

                if (parameter.ParameterType.Type == typeof(CancellationToken))
                {
                    var senderGetter = (Getter<TArgumentState, CancellationToken>)(object)parameter.GetGetter();
                    return new FunctionParameterGetter<TArgumentState>((
                        ref state,
                        _,
                        ref _,
                        ref cancellationToken) =>
                    {
                        cancellationToken = senderGetter(ref state);
                    });
                }

                JsonConverter<TParameter> paramConverter = GetOrAddConverter(parameter.ParameterType);
                var getter = parameter.GetGetter();
                return new FunctionParameterGetter<TArgumentState>((
                    ref state,
                    parameters,
                    ref _,
                    ref _) =>
                {
                    TParameter value = getter(ref state);
                    parameters[parameter.Name] = paramConverter.SerializeToElement(value);
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

            if (enumerableShape.Type == typeof(TElement[]) &&
                enumerableShape.ConstructionStrategy is CollectionConstructionStrategy.Parameterized)
            {
                return new JsonArrayConverter<TElement>(
                    elementConverter,
                    (IEnumerableTypeShape<TElement[], TElement>)(object)enumerableShape);
            }

            if (enumerableShape.Type == typeof(List<TElement>) &&
                enumerableShape.ConstructionStrategy is CollectionConstructionStrategy.Mutable)
            {
                return new JsonListConverter<TElement>(
                    elementConverter,
                    (IEnumerableTypeShape<List<TElement>, TElement>)(object)enumerableShape);
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
            return new JsonSurrogateConverter<T, TSurrogate>(surrogateShape.Marshaler, surrogateConverter);
        }

        public override object? VisitUnion<TUnion>(IUnionTypeShape<TUnion> unionShape, object? state)
        {
            var getUnionCaseIndex = unionShape.GetGetUnionCaseIndex();
            if (unionShape.UnionKind is UnionTypeShapeKind.CSharpUnion)
            {
                // C# unions use System.Text.Json-style untagged payloads rather than
                // the tagged schema used for type hierarchies and F# unions below.
                var cases = unionShape.UnionCases
                    .Select(unionCase => (JsonCSharpUnionCaseConverter<TUnion>)unionCase.Accept(this, unionShape)!)
                    .ToArray();

                return new JsonCSharpUnionConverter<TUnion>(getUnionCaseIndex, cases);
            }

            var baseTypeConverter = (JsonConverter<TUnion>)unionShape.BaseType.Invoke(this)!;
            var unionCases = unionShape.UnionCases
                .Select(unionCase => (JsonUnionCaseConverter<TUnion>)unionCase.Accept(this, null)!)
                .ToArray();

            return new JsonUnionConverter<TUnion>(getUnionCaseIndex, baseTypeConverter, unionCases);
        }

        public override object? VisitUnionCase<TUnionCase, TUnion>(IUnionCaseShape<TUnionCase, TUnion> unionCaseShape, object? state)
        {
            if (state is IUnionTypeShape { UnionKind: UnionTypeShapeKind.CSharpUnion })
            {
                JsonConverter<TUnionCase> csharpCaseConverter = GetOrAddConverter(unionCaseShape.UnionCaseType);
                return new JsonCSharpUnionCaseConverter<TUnionCase, TUnion>(
                    csharpCaseConverter, unionCaseShape.Marshaler, unionCaseShape.IsNullable);
            }

            var caseConverter = (JsonConverter<TUnionCase>)unionCaseShape.UnionCaseType.Accept(this)!;
            return new JsonUnionCaseConverter<TUnionCase, TUnion>(unionCaseShape.Name, unionCaseShape.Marshaler, caseConverter);
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

        public override object? VisitEvent<TDeclaringType, TEventHandler>(IEventShape<TDeclaringType, TEventHandler> eventShape, object? state = null)
        {
            var config = ((bool RequireAsync, object? Target))state!;
            if (config.RequireAsync != eventShape.HandlerType.IsAsync)
            {
                throw new ArgumentException("The event handler type does not match the expected async state.", nameof(eventShape));
            }

            if (!eventShape.IsStatic && config.Target is null)
            {
                throw new ArgumentException("A target instance must be provided for instance events.");
            }

            var addHandler = eventShape.GetAddHandler();
            var removeHandler = eventShape.GetRemoveHandler();

            if (config.RequireAsync)
            {
                var wrapEventHandler = (Func<AsyncJsonEventHandler, TEventHandler>)eventShape.HandlerType.Accept(this, state: eventShape)!;
                return new AsyncJsonEvent<TDeclaringType, TEventHandler>(config.Target, wrapEventHandler, addHandler, removeHandler);
            }
            else
            {
                var wrapEventHandler = (Func<JsonEventHandler, TEventHandler>)eventShape.HandlerType.Accept(this, state: eventShape)!;
                return new JsonEvent<TDeclaringType, TEventHandler>(config.Target, wrapEventHandler, addHandler, removeHandler);
            }
        }

        public override object? VisitFunction<TFunction, TArgumentState, TResult>(IFunctionTypeShape<TFunction, TArgumentState, TResult> functionShape, object? state = null)
        {
            if (state is IEventShape)
            {
                JsonConverter<TResult> resultConverter = GetOrAddConverter(functionShape.ReturnType);
                FunctionParameterGetter<TArgumentState>[] getParameters = functionShape.Parameters
                    .Select(p => (FunctionParameterGetter<TArgumentState>)p.Accept(this, functionShape)!)
                    .ToArray();

                if (functionShape.IsAsync)
                {
                    return new Func<AsyncJsonEventHandler, TFunction>(jsonHandler =>
                        functionShape.FromAsyncDelegate((ref argState) =>
                        {
                            object? sender = null;
                            CancellationToken cancellationToken = default;
                            Dictionary<string, JsonElement> parameters = new(getParameters.Length);
                            foreach (var getter in getParameters)
                            {
                                getter(ref argState, parameters, ref sender, ref cancellationToken);
                            }

                            ValueTask<JsonElement> result = jsonHandler(sender, parameters, cancellationToken);
                            return DeserializeResult(result);

                            // Work around CS1988 - Async methods cannot return by-ref-like types.
                            async ValueTask<TResult> DeserializeResult(ValueTask<JsonElement> task) =>
                                resultConverter.Deserialize(await task.ConfigureAwait(false))!;
                        })
                    );
                }
                else
                {
                    return new Func<JsonEventHandler, TFunction>(jsonHandler =>
                        functionShape.FromDelegate((ref argState) =>
                        {
                            object? sender = null;
                            CancellationToken cancellationToken = default;
                            Dictionary<string, JsonElement> parameters = new(getParameters.Length);
                            foreach (var getter in getParameters)
                            {
                                getter(ref argState, parameters, ref sender, ref cancellationToken);
                            }

                            JsonElement result = jsonHandler(sender, parameters);
                            return resultConverter.Deserialize(result)!;
                        })
                    );
                }
            }

            return new JsonObjectConverter<TFunction>([]);
        }

        private delegate void MethodParameterSetter<TArgumentState>(
            ref TArgumentState argumentState,
            IReadOnlyDictionary<string, JsonElement> parameters,
            CancellationToken cancellationToken);

        private delegate void FunctionParameterGetter<TArgumentState>(
            ref TArgumentState argumentState,
            Dictionary<string, JsonElement> parameters,
            ref object? sender,
            ref CancellationToken cancellationToken);

        internal static JsonValueType GetBuiltInValueTypes(Type type) =>
            type.IsEnum ? JsonValueType.String | JsonValueType.Number :
            s_defaultConverters.TryGetValue(type, out var builtIn) ? builtIn.ValueType : JsonValueType.None;

        internal static JsonValueType GetJsonValueType(JsonConverter converter)
        {
            if (converter is ISchematizedJsonConverter schematized)
            {
                return schematized.ValueType;
            }

            if (converter.Type is Type type && GetBuiltInValueTypes(type) is not JsonValueType.None and var valueType)
            {
                return valueType;
            }

            throw new NotSupportedException($"Converter '{converter.GetType()}' does not describe its JSON value type.");
        }

        private static readonly Dictionary<Type, (JsonConverter Converter, JsonValueType ValueType)> s_defaultConverters = new (JsonConverter Converter, JsonValueType ValueType)[]
        {
            (JsonMetadataServices.BooleanConverter, JsonValueType.Boolean),
            (JsonMetadataServices.SByteConverter, JsonValueType.Number),
            (JsonMetadataServices.Int16Converter, JsonValueType.Number),
            (JsonMetadataServices.Int32Converter, JsonValueType.Number),
            (JsonMetadataServices.Int64Converter, JsonValueType.Number),
            (JsonMetadataServices.ByteConverter, JsonValueType.Number),
            (JsonMetadataServices.ByteArrayConverter, JsonValueType.String),
            (JsonMetadataServices.UInt16Converter, JsonValueType.Number),
            (JsonMetadataServices.UInt32Converter, JsonValueType.Number),
            (JsonMetadataServices.UInt64Converter, JsonValueType.Number),
            (JsonMetadataServices.CharConverter, JsonValueType.String),
            (JsonMetadataServices.StringConverter, JsonValueType.String),
            (JsonMetadataServices.SingleConverter, JsonValueType.Number),
            (JsonMetadataServices.DoubleConverter, JsonValueType.Number),
            (JsonMetadataServices.DecimalConverter, JsonValueType.Number),
            (JsonMetadataServices.DateTimeConverter, JsonValueType.String),
            (JsonMetadataServices.DateTimeOffsetConverter, JsonValueType.String),
            (JsonMetadataServices.TimeSpanConverter, JsonValueType.String),
#if NET
            (JsonMetadataServices.Int128Converter, JsonValueType.Number),
            (JsonMetadataServices.UInt128Converter, JsonValueType.Number),
            (JsonMetadataServices.HalfConverter, JsonValueType.Number),
            (JsonMetadataServices.DateOnlyConverter, JsonValueType.String),
            (JsonMetadataServices.TimeOnlyConverter, JsonValueType.String),
            (new RuneConverter(), JsonValueType.String),
#endif
            (JsonMetadataServices.GuidConverter, JsonValueType.String),
            (JsonMetadataServices.UriConverter, JsonValueType.String),
            (JsonMetadataServices.VersionConverter, JsonValueType.String),
            (new BigIntegerConverter(), JsonValueType.Number),
        }.ToDictionary(entry => entry.Converter.Type!);
    }
}
