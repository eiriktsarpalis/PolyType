using Microsoft.Extensions.Configuration;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using PolyType.Abstractions;
using PolyType.Examples.ConfigurationBinder;
using PolyType.Examples.JsonSerializer;
using PolyType.Examples.StructuralEquality;
using Xunit;

namespace PolyType.Tests;

public abstract class ConfigurationBinderTests(ProviderUnderTest providerUnderTest)
{
    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void BoundResultEqualsOriginalValue<T>(TestCase<T> testCase)
    {
        ITypeShape<T> shape = providerUnderTest.ResolveShape(testCase);
        Func<IConfiguration, T?> binder = ConfigurationBinderTS.Create(shape);
        IEqualityComparer<T> comparer = StructuralEqualityComparer.Create(shape);
        (IConfiguration configuration, string json) = CreateConfiguration(testCase, shape);

        // In Microsoft.Extensions.Configuration 10.x, JSON `null` literals and `{}`
        // both surface as IConfigurationSection.Value == null with no children, so
        // values whose JSON serialization collapses to one of these round-trip as
        // the default of T. See:
        // https://learn.microsoft.com/dotnet/core/compatibility/extensions/10.0/configuration-null-values-preserved
        bool sectionIsNull = json is "null" or "{}";

        if (!providerUnderTest.HasConstructor(testCase) && !sectionIsNull)
        {
            Assert.Throws<NotSupportedException>(() => binder(configuration));
            return;
        }

        T? result = binder(configuration);

        if (sectionIsNull)
        {
            Assert.Equal(default, result);
        }
        else if (json.Contains("{}"))
        {
            // Nested empty objects are indistinguishable from nulls in MEC 10.x
            // so we settle for verifying that the binder runs without throwing.
        }
        else
        {
            Assert.Equal(testCase.Value, result, comparer!);
        }
    }

    [Theory]
    [MemberData(nameof(GetTaggedUnionCases))]
    public void CSharpUnionsUseTaggedConfiguration<T>(TestCase<T> testCase, string expectedJson)
    {
        ITypeShape<T> shape = providerUnderTest.ResolveShape(testCase);
        (_, string json) = CreateConfiguration(testCase, shape);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expectedJson), JsonNode.Parse(json)));
        BoundResultEqualsOriginalValue(testCase);
    }

    public static IEnumerable<object[]> GetTaggedUnionCases()
    {
        yield return [TestCase.Create(new CSharpScalarUnion(42)), """{"$type":"Int32","$values":42}"""];
        yield return [TestCase.Create(new CSharpRecursiveUnion(new CSharpRecursiveUnion(true))), """{"$type":"CSharpRecursiveUnion","$values":{"$type":"Boolean","$values":true}}"""];
        yield return [TestCase.Create(new CSharpHierarchyUnion(new CSharpDog("Fido", 5))), """{"$type":"CSharpDog","Name":"Fido","BarkVolume":5}"""];
    }

    [Fact]
    public void InvalidEmptyUnionPayloadsAreNotEncodedAsNull()
    {
        var testCase = TestCase.Create(default(CSharpValueUnion));
        ITypeShape<CSharpValueUnion> shape = providerUnderTest.ResolveShape(testCase);
        Assert.ThrowsAny<InvalidOperationException>(() => CreateConfiguration(testCase, shape));
    }
    
    private static (IConfiguration Configuration, string Json) CreateConfiguration<T>(TestCase<T> testCase, ITypeShape<T> shape)
    {
        string json;
        if (shape is IUnionTypeShape { UnionKind: UnionTypeShapeKind.CSharpUnion } or
            IOptionalTypeShape { ElementType: IUnionTypeShape { UnionKind: UnionTypeShapeKind.CSharpUnion } })
        {
            json = new TaggedUnionEncoder().Encode(shape, testCase.Value)?.ToJsonString() ?? "null";
        }
        else
        {
            JsonConverter<T> converter = JsonSerializerTS.CreateConverter(shape);
            json = converter.Serialize(testCase.Value);
            if (testCase.IsStack)
            {
                T? value = converter.Deserialize(json.AsSpan());
                json = converter.Serialize(value);
            }
        }
        
        string rootJson = $$"""{ "Root" : {{json}} }""";
        var builder = new ConfigurationBuilder();
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(rootJson));
        builder.AddJsonStream(stream);
        return (builder.Build().GetSection("Root"), json);
    }

    private sealed class TaggedUnionEncoder : TypeShapeVisitor
    {
        public JsonNode? Encode<T>(ITypeShape<T> shape, T? value)
        {
            if (shape is not IUnionTypeShape<T> { UnionKind: UnionTypeShapeKind.CSharpUnion } union)
            {
                if (shape is IOptionalTypeShape)
                {
                    return (JsonNode?)shape.Accept(this, value);
                }

                JsonNode? json = JsonNode.Parse(JsonSerializerTS.CreateConverter(shape).Serialize(value));
                return shape is IEnumerableTypeShape && json is JsonArray
                    ? (JsonNode?)shape.Accept(this, value)
                    : json;
            }

            int index = union.GetGetUnionCaseIndex()(ref value!);
            return (JsonNode?)union.UnionCases[index].Accept(this, value);
        }

        public override object? VisitOptional<TOptional, TElement>(IOptionalTypeShape<TOptional, TElement> shape, object? state = null) =>
            shape.GetDeconstructor()((TOptional)state!, out TElement? value) ? Encode(shape.ElementType, value) : null;

        public override object? VisitEnumerable<TEnumerable, TElement>(IEnumerableTypeShape<TEnumerable, TElement> shape, object? state = null)
        {
            JsonArray array = new();
            foreach (TElement value in shape.GetGetEnumerable()((TEnumerable)state!))
            {
                array.Add(Encode(shape.ElementType, value));
            }

            return array;
        }

        public override object? VisitUnionCase<TUnionCase, TUnion>(
            IUnionCaseShape<TUnionCase, TUnion> unionCase, object? state = null)
        {
            TUnionCase? payload = unionCase.Marshaler.Unmarshal((TUnion?)state);
            if (payload is null)
            {
                // IConfiguration represents null sections as the default of their declared type.
                return null;
            }

            JsonNode? json = Encode(unionCase.UnionCaseType, payload);
            if (json is JsonObject obj && typeof(TUnionCase) != typeof(object) &&
                unionCase.UnionCaseType is IObjectTypeShape or IDictionaryTypeShape)
            {
                obj.Insert(0, "$type", unionCase.Name);
                return obj;
            }

            return new JsonObject
            {
                ["$type"] = unionCase.Name,
                ["$values"] = json,
            };
        }
    }
}

public sealed class ConfigurationBinderTests_Reflection() : ConfigurationBinderTests(ReflectionProviderUnderTest.NoEmit);
public sealed class ConfigurationBinderTests_ReflectionEmit() : ConfigurationBinderTests(ReflectionProviderUnderTest.Emit);
public sealed class ConfigurationBinderTests_SourceGen() : ConfigurationBinderTests(SourceGenProviderUnderTest.Default);