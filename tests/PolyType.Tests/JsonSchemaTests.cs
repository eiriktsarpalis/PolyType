using Json.Schema;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using PolyType.Abstractions;
using PolyType.Examples.JsonSchema;
using PolyType.Examples.JsonSerializer;
using Xunit;
using Xunit.Sdk;

namespace PolyType.Tests;

public abstract class JsonSchemaTests(ProviderUnderTest providerUnderTest)
{
    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void GeneratesExpectedSchema(ITestCase testCase)
    {
        ITypeShape shape = providerUnderTest.ResolveShape(testCase);
        JsonObject schema = JsonSchemaGenerator.Generate(shape);

        switch (shape)
        {
            case IEnumTypeShape enumShape:
                AssertType("string");
                if (enumShape.Type.GetCustomAttribute<FlagsAttribute>() != null)
                {
                    Assert.DoesNotContain("enum", schema);
                }
                else
                {
                    Assert.Equal(Enum.GetNames(enumShape.Type), schema["enum"]!.AsArray().Select(node => (string)node!));
                }
                break;

            case IOptionalTypeShape nullableShape:
                JsonObject nullableElementSchema = JsonSchemaGenerator.Generate(nullableShape.ElementType);
                schema.Remove("type");
                nullableElementSchema.Remove("type");
                Assert.True(JsonNode.DeepEquals(nullableElementSchema, schema));
                break;
            
            case ISurrogateTypeShape surrogateShape:
                JsonObject surrogateSchema = JsonSchemaGenerator.Generate(surrogateShape.SurrogateType);
                Assert.True(JsonNode.DeepEquals(surrogateSchema, schema));
                break;

            case IEnumerableTypeShape enumerableShape:
                if (enumerableShape.Type == typeof(byte[]))
                {
                    AssertType("string");
                    break;
                }

                AssertType("array");
                JsonObject elementSchema = JsonSchemaGenerator.Generate(enumerableShape.ElementType);
                for (int i = 0; i < enumerableShape.Rank; i++) schema = (JsonObject)schema["items"]!;
                Assert.True(JsonNode.DeepEquals(elementSchema, schema));
                break;

            case IDictionaryTypeShape dictionaryShape:
                AssertType("object");
                JsonObject valueSchema = JsonSchemaGenerator.Generate(dictionaryShape.ValueType);
                Assert.True(JsonNode.DeepEquals(valueSchema, schema["additionalProperties"]));
                break;

            case IObjectTypeShape objectShape:
                if (objectShape.Properties is not [])
                {
                    AssertType("object");
                    Assert.Contains("properties", schema);
                }
                else
                {
                    Assert.DoesNotContain("properties", schema);
                    Assert.DoesNotContain("required", schema);
                }
                break;

            case IUnionTypeShape unionCaseShape:
                Assert.Contains("anyOf", schema);
                break;

            default:
                Assert.Empty(schema);
                break;
        }

        void AssertType(string type)
        {
            JsonNode? typeValue = Assert.Contains("type", schema);
            if (!shape.Type.IsValueType || Nullable.GetUnderlyingType(shape.Type) != null)
            {
                Assert.Equal([type, "null"], ((JsonArray)typeValue!).Select(x => (string)x!));
            }
            else
            {
                Assert.Equal(type, (string)typeValue!);
            }
        }
    }

    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void SchemaMatchesJsonSerializer<T>(TestCase<T> testCase)
    {
#if NET8_0_OR_GREATER
        if (typeof(T) == typeof(Int128) || typeof(T) == typeof(UInt128) ||
            typeof(T) == typeof(Int128?) || typeof(T) == typeof(UInt128?))
        {
            return; // Not supported by JsonSchema.NET
        }
#endif

        ITypeShape<T> shape = providerUnderTest.ResolveShape(testCase);
        JsonObject schema = JsonSchemaGenerator.Generate(shape);
        string json = JsonSerializerTS.CreateConverter(shape).Serialize(testCase.Value);

        JsonSchema jsonSchema = JsonSerializer.Deserialize<JsonSchema>(schema)!;
        EvaluationOptions options = new() { OutputFormat = OutputFormat.List };
        EvaluationResults results = jsonSchema.Evaluate(JsonNode.Parse(json), options);
        if (!results.IsValid)
        {
            IEnumerable<string> errors = results.Details
                .Where(d => d.HasErrors)
                .SelectMany(d => d.Errors!.Select(error => $"Path:${d.InstanceLocation} {error.Key}:{error.Value}"));

            throw new XunitException($"""
                Instance JSON document does not match the specified schema.
                Schema:
                {JsonSerializer.Serialize(schema)}
                Instance:
                {json}
                Errors:
                {string.Join(Environment.NewLine, errors)}
                """);
        }
    }

    [Fact]
    public void TestMethodShapeSchema()
    {
        ITypeShape serviceShape = providerUnderTest.Provider.Resolve<RpcService>();
        IMethodShape getEventsAsync = serviceShape.Methods.Single(m => m.Name == nameof(RpcService.GetEventsAsync));
        IMethodShape resetAsync = serviceShape.Methods.Single(m => m.Name == nameof(RpcService.ResetAsync));

        JsonNode? actualSchema = JsonSchemaGenerator.Generate(getEventsAsync);
        JsonNode? expectedSchema = JsonNode.Parse("""
            {
                "name": "GetEventsAsync",
                "type": "object",
                "properties": {
                    "count": { "type": "integer" }
                },
                "required": ["count"],
                "output": {
                    "type": ["array","null"],
                    "items": {
                        "type": ["object","null"],
                        "properties": {
                            "id": { "type": "integer" }
                        },
                        "required": ["id"]
                    }
                }
            }
            """);

        Assert.True(JsonNode.DeepEquals(expectedSchema, actualSchema));

        actualSchema = JsonSchemaGenerator.Generate(resetAsync);
        expectedSchema = JsonNode.Parse("""
            {
                "name": "ResetAsync",
                "type": "object",
                "output": { }
            }
            """);

        Assert.True(JsonNode.DeepEquals(expectedSchema, actualSchema));
    }
}

public sealed class JsonSchemaTests_Reflection() : JsonSchemaTests(ReflectionProviderUnderTest.NoEmit);
public sealed class JsonSchemaTests_ReflectionEmit() : JsonSchemaTests(ReflectionProviderUnderTest.Emit);
public sealed class JsonSchemaTests_SourceGen() : JsonSchemaTests(SourceGenProviderUnderTest.Default);