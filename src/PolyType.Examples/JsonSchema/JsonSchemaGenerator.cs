using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using PolyType.Abstractions;

namespace PolyType.Examples.JsonSchema;

/// <summary>
/// A JSON schema generator for .NET types inspired by https://github.com/eiriktsarpalis/stj-schema-mapper
/// </summary>
public static class JsonSchemaGenerator
{
    /// <summary>
    /// Generates a JSON schema using the specified shape provider.
    /// </summary>
    /// <typeparam name="T">The type for which to generate a JSON schema.</typeparam>
    public static JsonObject Generate<T>(ITypeShapeProvider typeShapeProvider)
        => Generate(typeShapeProvider.GetTypeShapeOrThrow<T>());

    private const string MetaSchemaUri = "https://json-schema.org/draft/2020-12/schema";

    /// <summary>
    /// Generates a JSON schema using the specified shape.
    /// </summary>
    public static JsonObject Generate(ITypeShape typeShape)
        => new Generator().GenerateSchema(typeShape);

    /// <summary>
    /// Generates a JSON schema using the specified method shape.
    /// </summary>
    public static JsonObject Generate(IMethodShape methodShape)
        => new Generator().GenerateMethodSchema(methodShape);

#if NET
    /// <summary>
    /// Generates a JSON schema using an externally provided <see cref="IShapeable{T}"/> implementation.
    /// </summary>
    /// <typeparam name="T">The type for which to generate a JSON schema.</typeparam>
    /// <typeparam name="TProvider">The type providing an <see cref="IShapeable{T}"/> implementation.</typeparam>
    public static JsonObject Generate<T, TProvider>() where TProvider : IShapeable<T>
        => Generate(TProvider.GetTypeShape());

    /// <summary>
    /// Generates a JSON schema using its <see cref="IShapeable{T}"/> implementation.
    /// </summary>
    /// <typeparam name="T">The type for which to generate a JSON schema.</typeparam>
    public static JsonObject Generate<T>() where T : IShapeable<T>
        => Generate(T.GetTypeShape());
#endif

    private sealed class Generator
    {
        private readonly Dictionary<(Type, bool AllowNull), string> _locations = new();
        private readonly List<string> _path = new();

        public JsonObject GenerateMethodSchema(IMethodShape methodShape)
        {
            JsonObject? parameterSchemas = null;
            JsonArray? requiredParams = null;
            foreach (var parameter in methodShape.Parameters)
            {
                if (parameter.ParameterType.Type == typeof(CancellationToken))
                {
                    continue;
                }

                Push("properties");
                Push(parameter.Name);
                (parameterSchemas ??= []).Add(parameter.Name, GenerateSchema(parameter.ParameterType, depth: 1));
                Pop();
                Pop();
                if (parameter.IsRequired)
                {
                    (requiredParams ??= []).Add((JsonNode)parameter.Name);
                }
            }

            JsonObject functionSchema = new JsonObject
            {
                ["name"] = methodShape.Name,
                ["type"] = "object",
            };

            if (parameterSchemas is not null)
            {
                functionSchema["properties"] = parameterSchemas;
            }

            if (requiredParams is not null)
            {
                functionSchema["required"] = requiredParams;
            }

            Push("output");
            functionSchema["output"] = GenerateSchema(methodShape.ReturnType, depth: 1);
            Pop();
            return CompleteDocument(functionSchema, allowNull: false, depth: 0);
        }

        public JsonObject GenerateSchema(ITypeShape typeShape, bool allowNull = true, bool cacheLocation = true, int depth = 0)
        {
            allowNull = allowNull && IsNullableType(typeShape.Type);

            if (s_simpleTypeInfo.TryGetValue(typeShape.Type, out SimpleTypeJsonSchema simpleType))
            {
                return CompleteDocument(simpleType.ToSchemaDocument(), allowNull, depth);
            }

            if (cacheLocation)
            {
                var key = (typeShape.Type, allowNull);
                if (_locations.TryGetValue(key, out string? location))
                {
                    return new JsonObject
                    {
                        ["$ref"] = (JsonNode)location!,
                    };
                }
                else
                {
                    _locations[key] = _path.Count == 0 ? "#" : $"#/{string.Join("/", _path)}";
                }
            }

            JsonObject schema;
            switch (typeShape)
            {
                case IEnumTypeShape enumShape:
                    schema = new JsonObject { ["type"] = "string" };
                    if (!enumShape.IsFlags)
                    {
                        schema["enum"] = CreateArray(Enum.GetNames(enumShape.Type).Select(name => (JsonNode)name));
                    }

                    break;

                case IOptionalTypeShape optionalShape:
                    schema = GenerateSchema(optionalShape.ElementType, cacheLocation: false, depth: depth + 1);
                    allowNull = true;
                    break;
                
                case ISurrogateTypeShape surrogateShape:
                    return CompleteDocument(GenerateSchema(surrogateShape.SurrogateType, cacheLocation: false, depth: depth + 1), allowNull: false, depth);

                case IEnumerableTypeShape enumerableShape:
                    for (int i = 0; i < enumerableShape.Rank; i++)
                    {
                        Push("items");
                    }

                    schema = GenerateSchema(enumerableShape.ElementType, depth: depth + 1);

                    for (int i = 0; i < enumerableShape.Rank; i++)
                    {
                        schema = new JsonObject
                        {
                            ["type"] = "array",
                            ["items"] = schema,
                        };

                        Pop();
                    }

                    break;

                case IDictionaryTypeShape dictionaryShape:
                    Push("additionalProperties");
                    JsonObject additionalPropertiesSchema = GenerateSchema(dictionaryShape.ValueType, depth: depth + 1);
                    Pop();

                    schema = new JsonObject
                    {
                        ["type"] = "object",
                        ["additionalProperties"] = additionalPropertiesSchema,
                    };

                    break;

                case IObjectTypeShape objectShape:
                    schema = new();

                    if (objectShape.Properties is not [])
                    {
                        IConstructorShape? ctor = objectShape.Constructor;
                        Dictionary<string, IParameterShape>? ctorParams = ctor?.Parameters
                            .Where(p => p.Kind is ParameterKind.MethodParameter || p.IsRequired)
                            .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

                        JsonObject properties = new();
                        JsonArray? required = null;

                        Push("properties");
                        foreach (IPropertyShape prop in objectShape.Properties)
                        {
                            IParameterShape? associatedParameter = null;
                            ctorParams?.TryGetValue(prop.Name, out associatedParameter);

                            bool isNonNullable = 
                                (!prop.HasGetter || prop.IsGetterNonNullable) &&
                                (!prop.HasSetter || prop.IsSetterNonNullable) &&
                                (associatedParameter is null || associatedParameter.IsNonNullable);
                            
                            Push(prop.Name);
                            JsonObject propSchema = GenerateSchema(prop.PropertyType, allowNull: !isNonNullable, depth: depth + 1);
                            Pop();

                            properties.Add(prop.Name, propSchema);
                            if (associatedParameter?.IsRequired is true)
                            {
                                (required ??= new()).Add((JsonNode)prop.Name);
                            }
                        }
                        Pop();

                        schema["type"] = "object";
                        schema["properties"] = properties;
                        if (required != null)
                        {
                            schema["required"] = required;
                        }
                    }

                    break;

                case IUnionTypeShape unionShape:
                    JsonArray anyOf = new();
                    Push("anyOf");

                    bool unionCasesContainBaseType = false;
                    foreach (IUnionCaseShape caseShape in unionShape.UnionCases)
                    {
                        Push($"{anyOf.Count}");
                        JsonObject caseSchema = GenerateSchema(caseShape.UnionCaseType, cacheLocation: false, depth: depth + 1);
                        Pop();

                        if (caseShape.UnionCaseType is IObjectTypeShape or IDictionaryTypeShape)
                        {
                            // Schema is already an object schema, embed discriminator inside it
                            JsonObject properties;
                            if (caseSchema.TryGetPropertyValue("properties", out JsonNode? propertiesValue))
                            {
                                properties = (JsonObject)propertiesValue!;
                            }
                            else
                            {
                                caseSchema["properties"] = properties = new JsonObject();
                            }

                            JsonArray required;
                            if (caseSchema.TryGetPropertyValue("required", out JsonNode? requiredValue))
                            {
                                required = (JsonArray)requiredValue!;
                            }
                            else
                            {
                                caseSchema["required"] = required = new JsonArray();
                            }

                            properties["$type"] = new JsonObject { ["const"] = caseShape.Name };
                            required.Add((JsonNode)"$type");
                        }
                        else
                        {
                            // Embed in the schema for an envelope type.
                            caseSchema = new JsonObject
                            {
                                ["properties"] = new JsonObject
                                {
                                    ["$type"] = new JsonObject { ["const"] = caseShape.Name },
                                    ["$values"] = caseSchema
                                },
                                ["required"] = new JsonArray { (JsonNode)"$type", (JsonNode)"$values" }
                            };
                        }

                        anyOf.Add((JsonNode)caseSchema);

                        if (caseShape.UnionCaseType.Type == unionShape.Type)
                        {
                            unionCasesContainBaseType = true;
                        }
                    }

                    if (!unionCasesContainBaseType)
                    {
                        Push($"{anyOf.Count}");
                        JsonNode caseSchema = GenerateSchema(unionShape.BaseType, cacheLocation: false, depth: depth + 1);
                        Pop();

                        anyOf.Add(caseSchema);
                    }

                    schema = new JsonObject
                    {
                        ["anyOf"] = anyOf,
                    };

                    Pop();
                    break;

                default:
                    schema = new JsonObject();
                    break;
            }

            return CompleteDocument(schema, allowNull, depth);
        }

        private static JsonObject CompleteDocument(JsonObject schema, bool allowNull, int depth)
        {
            if (allowNull && schema.TryGetPropertyValue("type", out JsonNode? typeValue))
            {
                if (typeValue is JsonArray types)
                {
                    types.Add((JsonNode)"null");
                }
                else
                {
                    schema["type"] = new JsonArray { (JsonNode)(string)typeValue!, (JsonNode)"null" };
                }
            }

            if (depth == 0)
            {
                schema.Insert(0, "$schema", MetaSchemaUri);
            }

            return schema;
        }

        private void Push(string name)
        {
            _path.Add(name);
        }

        private void Pop()
        {
            _path.RemoveAt(_path.Count - 1);
        }

        private static JsonArray CreateArray(IEnumerable<JsonNode> elements)
        {
            var arr = new JsonArray();
            foreach (JsonNode elem in elements)
            {
                arr.Add(elem);
            }

            return arr;
        }

        private static bool IsNullableType(Type type)
        {
            return !type.IsValueType || Nullable.GetUnderlyingType(type) != null;
        }

        private readonly struct SimpleTypeJsonSchema
        {
            public SimpleTypeJsonSchema(string? type, string? format = null, string? pattern = null)
            {
                Type = type;
                Format = format;
                Pattern = pattern;
            }

            public string? Type { get; }
            public string? Format { get; }
            public string? Pattern { get; }

            public JsonObject ToSchemaDocument()
            {
                var schema = new JsonObject();
                if (Type != null)
                {
                    schema["type"] = Type;
                }

                if (Format != null)
                {
                    schema["format"] = Format;
                }

                if (Pattern != null)
                {
                    schema["pattern"] = Pattern;
                }

                return schema;
            }
        }

        private static readonly Dictionary<Type, SimpleTypeJsonSchema> s_simpleTypeInfo = new()
        {
            [typeof(object)] = new(),
            [typeof(bool)] = new("boolean"),
            [typeof(byte)] = new("integer"),
            [typeof(ushort)] = new("integer"),
            [typeof(uint)] = new("integer"),
            [typeof(ulong)] = new("integer"),
            [typeof(sbyte)] = new("integer"),
            [typeof(short)] = new("integer"),
            [typeof(int)] = new("integer"),
            [typeof(long)] = new("integer"),
            [typeof(float)] = new("number"),
            [typeof(double)] = new("number"),
            [typeof(decimal)] = new("number"),
            [typeof(char)] = new("string"),
            [typeof(string)] = new("string"),
            [typeof(byte[])] = new("string"),
            [typeof(Memory<byte>)] = new("string"),
            [typeof(ReadOnlyMemory<byte>)] = new("string"),
            [typeof(DateTime)] = new("string", format: "date-time"),
            [typeof(DateTimeOffset)] = new("string", format: "date-time"),
            [typeof(TimeSpan)] = new("string", pattern: @"^-?(\d+\.)?\d{2}:\d{2}:\d{2}(\.\d{1,7})?$"),
#if NET
            [typeof(Half)] = new("number"),
            [typeof(UInt128)] = new("integer"),
            [typeof(Int128)] = new("integer"),
            [typeof(DateOnly)] = new("string", format: "date"),
            [typeof(TimeOnly)] = new("string", format: "time"),
#endif
            [typeof(Guid)] = new("string", format: "uuid"),
            [typeof(Uri)] = new("string", format: "uri"),
            [typeof(Version)] = new("string"),
            [typeof(JsonDocument)] = new(),
            [typeof(JsonElement)] = new(),
            [typeof(JsonNode)] = new(),
            [typeof(JsonValue)] = new(),
            [typeof(JsonObject)] = new("object"),
            [typeof(JsonArray)] = new("array"),
        };
    }
}
