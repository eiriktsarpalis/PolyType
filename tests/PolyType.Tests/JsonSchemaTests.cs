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

        // Every root schema must declare the JSON Schema version.
        Assert.Equal("https://json-schema.org/draft/2020-12/schema", (string?)schema["$schema"]);

        switch (shape)
        {
            case IEnumTypeShape enumShape:
                JsonArray enumAlternatives = Assert.IsType<JsonArray>(schema["anyOf"]);
                Assert.Equal(2, enumAlternatives.Count);
                JsonObject enumNames = Assert.IsType<JsonObject>(enumAlternatives[0]);
                Assert.Equal("string", (string?)enumNames["type"]);
                Assert.Equal("integer", (string?)enumAlternatives[1]!["type"]);
                if (enumShape.IsFlags)
                {
                    Assert.DoesNotContain("enum", enumNames);
                }
                else
                {
                    Assert.Equal(Enum.GetNames(enumShape.Type), enumNames["enum"]!.AsArray().Select(node => (string)node!));
                }
                break;

            case IOptionalTypeShape nullableShape:
                JsonObject nullableElementSchema = JsonSchemaGenerator.Generate(nullableShape.ElementType);
                if (schema["anyOf"] is JsonArray nullableAlternatives)
                {
                    Assert.Equal("null", (string?)nullableAlternatives[nullableAlternatives.Count - 1]!["type"]);
                    nullableAlternatives.RemoveAt(nullableAlternatives.Count - 1);
                }

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
                JsonNode? itemsSchema = schema;
                for (int i = 0; i < enumerableShape.Rank; i++) itemsSchema = ((JsonObject)itemsSchema!)["items"];
                elementSchema.Remove("$schema");
                Assert.True(JsonNode.DeepEquals(elementSchema, itemsSchema));
                break;

            case IDictionaryTypeShape dictionaryShape:
                AssertType("object");
                JsonObject valueSchema = JsonSchemaGenerator.Generate(dictionaryShape.ValueType);
                valueSchema.Remove("$schema");
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
                Assert.Single(schema);
                Assert.Contains("$schema", schema);
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
    [InlineData(BindingFlags.Public)]
    [InlineData(BindingFlags.Public | BindingFlags.Static)]
    [InlineData((BindingFlags)0x40000000)]
    public void FlagsSchemaMatchesNamedAndNumericOutput(BindingFlags value)
    {
        ITypeShape<BindingFlags> shape = providerUnderTest.Provider.GetTypeShapeOrThrow<BindingFlags>();
        string json = JsonSerializerTS.CreateConverter(shape).Serialize(value);
        JsonSchema schema = JsonSchema.FromText(JsonSchemaGenerator.Generate(shape).ToJsonString());
        using JsonDocument document = JsonDocument.Parse(json);

        Assert.True(schema.Evaluate(document.RootElement).IsValid);
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
        if (typeof(T) == typeof(CSharpNumericUnion) ||
            typeof(T) == typeof(CSharpAmbiguousNumericUnion) ||
            typeof(T) == typeof(CSharpNullableValueUnion) ||
            typeof(T) == typeof(CSharpObjectUnion) ||
            typeof(T) == typeof(CSharpHierarchyUnion) ||
            typeof(T) == typeof(CSharpConstructorUnion))
        {
            // These unions have overlapping JSON categories (e.g. int and long both map to numbers).
            // The converter rejects that ambiguity on first read/write, rather than at construction.
            var converter = JsonSerializerTS.CreateConverter(shape);
            Assert.Throws<InvalidOperationException>(() => converter.Serialize(testCase.Value));
            return;
        }

        string json = JsonSerializerTS.CreateConverter(shape).Serialize(testCase.Value);

        JsonSchema jsonSchema = JsonSchema.FromText(JsonSerializer.Serialize(schema));
        EvaluationOptions options = new() { OutputFormat = OutputFormat.List };
        using JsonDocument instanceDoc = JsonDocument.Parse(json);
        EvaluationResults results = jsonSchema.Evaluate(instanceDoc.RootElement, options);
        if (!results.IsValid)
        {
            IEnumerable<string> errors = (results.Details ?? [])
                .Where(d => d.Errors is { Count: > 0 })
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
        ITypeShape serviceShape = providerUnderTest.Provider.GetTypeShapeOrThrow<RpcService>();
        IMethodShape getEventsAsync = serviceShape.Methods.Single(m => m.Name == nameof(RpcService.GetEventsAsync));
        IMethodShape resetAsync = serviceShape.Methods.Single(m => m.Name == nameof(RpcService.ResetAsync));

        JsonNode? actualSchema = JsonSchemaGenerator.Generate(getEventsAsync);
        JsonNode? expectedSchema = JsonNode.Parse($$"""
            {
                "$schema": "https://json-schema.org/draft/2020-12/schema",
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
        expectedSchema = JsonNode.Parse($$"""
            {
                "$schema": "https://json-schema.org/draft/2020-12/schema",
                "name": "ResetAsync",
                "type": "object",
                "output": { }
            }
            """);

        Assert.True(JsonNode.DeepEquals(expectedSchema, actualSchema));
    }

    [Fact]
    public void RecursiveSchemaDescribesInlinePayloadWithoutSelfReference()
    {
        ITypeShape<CSharpRecursiveUnion> shape = providerUnderTest.ResolveShape(TestCase.Create(new CSharpRecursiveUnion(true)));
        string schema = JsonSchemaGenerator.Generate(shape).ToJsonString();
        Assert.Equal("""{"$schema":"https://json-schema.org/draft/2020-12/schema","anyOf":[{"type":"boolean"}]}""", schema);
    }

    [Theory]
    [MemberData(nameof(GetSchemaNullCases))]
    public void SchemaNullabilityFollowsPayloadCases<T>(TestCase<T> testCase, bool acceptsNull)
    {
        ITypeShape<T> shape = providerUnderTest.ResolveShape(testCase);
        string schema = JsonSchemaGenerator.Generate(shape).ToJsonString();
        Assert.Equal(acceptsNull, schema.Contains("\"null\""));
    }

    public static IEnumerable<object[]> GetSchemaNullCases()
    {
        yield return [TestCase.Create(new CSharpScalarUnion(42)), true];
        yield return [TestCase.Create(new CSharpValueUnion(42)), false];
    }

    [Fact]
    public void StructuralRecursionRetainsReferences()
    {
        ITypeShape<CSharpTreeUnion> shape = providerUnderTest.ResolveShape(TestCase.Create(new CSharpTreeUnion(true)));
        JsonObject schema = JsonSchemaGenerator.Generate(shape);
        Assert.Equal("#", (string?)schema["anyOf"]![1]!["items"]!["$ref"]);
    }
}

public sealed class JsonSchemaTests_Reflection() : JsonSchemaTests(ReflectionProviderUnderTest.NoEmit);
public sealed class JsonSchemaTests_ReflectionEmit() : JsonSchemaTests(ReflectionProviderUnderTest.Emit);
public sealed class JsonSchemaTests_SourceGen() : JsonSchemaTests(SourceGenProviderUnderTest.Default);
