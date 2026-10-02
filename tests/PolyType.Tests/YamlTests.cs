using System.Collections.Immutable;
using System.Numerics;
using PolyType.Examples.YamlSerializer;
using PolyType.Tests.FSharp;
using Xunit;

namespace PolyType.Tests;

public abstract class YamlTests(ProviderUnderTest providerUnderTest)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DictionaryEntries_SkipUnknownNodesAndAcceptReorderedFields(bool readOnly)
    {
        const string yaml = """
            - extra: [1, { nested: value }]
              value: 42
              key: 1
            - key: 2
              extra: { nested: [1, 2] }
              value: 43
            """;
        IReadOnlyDictionary<int, int>? value;
        if (readOnly)
        {
            var converter = YamlSerializer.CreateConverter<System.Collections.ObjectModel.ReadOnlyDictionary<int, int>>(providerUnderTest.Provider);
            value = converter.Deserialize(yaml);
        }
        else
        {
            var converter = YamlSerializer.CreateConverter<IDictionary<int, int>>(providerUnderTest.Provider);
            value = Assert.IsAssignableFrom<IReadOnlyDictionary<int, int>>(converter.Deserialize(yaml));
        }

        Assert.NotNull(value);
        Assert.Equal(new Dictionary<int, int> { [1] = 42, [2] = 43 }, value);
    }

    [Theory]
    [InlineData("ignored: text\nValue: 42")]
    [InlineData("ignored: [1, { nested: [2, 3] }]\nValue: 42")]
    [InlineData("ignored: { nested: [1, { deep: text }] }\nValue: 42")]
    public void UnknownProperties_AreSkippedWithoutConsumingTheNextProperty(string yaml)
    {
        var converter = YamlSerializer.CreateConverter(providerUnderTest.ResolveShape(TestCase.Create(new SimplePoco())));
        SimplePoco? value = converter.Deserialize(yaml);
        Assert.NotNull(value);
        Assert.Equal(42, value.Value);
    }

#if NET
    [Theory]
    [InlineData(0)]
    [InlineData(42)]
    public void ShapeableEntryPoints_InteroperateWithProviderConverters(int value)
    {
        SimpleRecord record = new(value);
        var converter = YamlSerializer.CreateConverter<SimpleRecord>(providerUnderTest.Provider);
        Assert.Equal(record, converter.Deserialize(YamlSerializer.Serialize(record)));
        Assert.Equal(record, YamlSerializer.Deserialize<SimpleRecord>(converter.Serialize(record)));
        Assert.Equal(value, YamlSerializer.Deserialize<int, Witness>(YamlSerializer.Serialize<int, Witness>(value)));
        Assert.Equal(record, YamlSerializer.CreateConverterUsingReflection<SimpleRecord>().Deserialize(converter.Serialize(record)));
    }
#endif

    [Theory]
    [MemberData(nameof(GetValuesAndExpectedEncoding))]
    public void ReturnsExpectedEncoding<T>(TestCase<T> testCase, string expectedEncoding)
    {
        YamlConverter<T> converter = GetConverterUnderTest(testCase);

        string yaml = converter.Serialize(testCase.Value);
        Assert.Equal(expectedEncoding, yaml);
    }

    [Theory]
    [MemberData(nameof(GetValuesAndExpectedEncoding))]
    public void ExpectedEncodingDeserializedToValue<T>(TestCase<T> testCase, string expectedEncoding)
    {
        YamlConverter<T> converter = GetConverterUnderTest(testCase);

        T? result = converter.Deserialize(expectedEncoding);
        if (testCase.IsEquatable)
        {
            Assert.Equal(testCase.Value, result);
        }
        else
        {
            Assert.Equal(expectedEncoding, converter.Serialize(result));
        }
    }

    public static IEnumerable<object?[]> GetValuesAndExpectedEncoding()
    {
        Witness p = new();
        yield return [TestCase.Create((object?)null, p), "null"];
        yield return [TestCase.Create(false, p), "false"];
        yield return [TestCase.Create(true, p), "true"];
        yield return [TestCase.Create(42, p), "42"];
        yield return [TestCase.Create(-7001, p), "-7001"];
        yield return [TestCase.Create((byte)255, p), "255"];
        yield return [TestCase.Create(int.MaxValue, p), "2147483647"];
        yield return [TestCase.Create(int.MinValue, p), "-2147483648"];
        yield return [TestCase.Create(long.MaxValue, p), "9223372036854775807"];
        yield return [TestCase.Create(long.MinValue, p), "-9223372036854775808"];
        yield return [TestCase.Create((BigInteger)long.MaxValue, p), "9223372036854775807"];
        yield return [TestCase.Create(decimal.MaxValue, p), "79228162514264337593543950335"];
        yield return [TestCase.Create('c', p), "c"];
        yield return [TestCase.Create((string?)null, p), "null"];
        yield return [TestCase.Create("", p), "''"];
        yield return [TestCase.Create("Hello World", p), "Hello World"];
        yield return [TestCase.Create(Guid.Empty, p), "00000000-0000-0000-0000-000000000000"];
        yield return [TestCase.Create(new SimpleRecord(value: 42)), "value: 42"];
#if NET
        yield return [TestCase.Create(Int128.MaxValue, p), "170141183460469231731687303715884105727"];
        yield return [TestCase.Create((Half)1, p), "1"];
#endif
        yield return [TestCase.Create((int[])[1, 2, 3], p), "- 1\n- 2\n- 3"];
        yield return [TestCase.Create((List<int>)[1, 2, 3], p), "- 1\n- 2\n- 3"];
        yield return
        [
            TestCase.Create(new Dictionary<string, string> { ["key1"] = "value", ["key2"] = "value" }, p),
            "- key: key1\n  value: value\n- key: key2\n  value: value"
        ];
        yield return [TestCase.Create(new PolymorphicClass(42)), "_type: PolymorphicClass\nInt: 42"];
        yield return [TestCase.Create<Tree>(new Tree.Leaf()), "_type: leaf"];
        yield return [TestCase.Create(FSharpUnion.NewC(42), p), "_type: C\nfoo: 42"];
        yield return [TestCase.Create(FSharpUnion.B, p), "_type: B"];
        yield return [TestCase.Create(FSharpStructUnion.NewC(42), p), "_type: C\nfoo: 42"];
        yield return [TestCase.Create(new CSharpScalarUnion(42)), "_type: Int32\n_value: 42"];
        yield return [TestCase.Create(default(CSharpScalarUnion)), "_type: String\n_value: null"];
        yield return [TestCase.Create(new CSharpArrayUnion(new int[] { 1, 2 })), "_type: Array_Int32\n_value:\n- 1\n- 2"];
    }

    [Fact]
    public void NullRepresentedFSharpCaseRetainsEmptyMapping()
    {
        var converter = GetConverterUnderTest(TestCase.Create(NullaryUnion.A, new Witness()));
        using YamlWriter writer = new();
        converter.Write(writer, NullaryUnion.A);

        string yaml = writer.ToString();
        Assert.Equal("_type: A", yaml);
        Assert.Null(converter.Deserialize(yaml));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClassUnionNullPayloadIsDistinctFromNullReference(bool nullReference)
    {
        var converter = GetConverterUnderTest(TestCase.Create(new CSharpClassUnion(42)));
        CSharpClassUnion? value = nullReference ? null : new((string?)null);
        string yaml = converter.Serialize(value);
        Assert.Equal(nullReference ? "null" : "_type: String\n_value: null", yaml);

        CSharpClassUnion? result = converter.Deserialize(yaml);
        Assert.Equal(nullReference, ReferenceEquals(result, null));
        if (!nullReference)
        {
            Assert.Null(result!.Value);
        }
    }

    [Fact]
    public void NullObjectPayloadsCannotBeMergedIntoTheCaseMapping()
    {
        var testCase = TestCase.Create(new CSharpConstructorUnion((CSharpAnimal?)null));
        var converter = GetConverterUnderTest(testCase);
        Assert.Throws<NotSupportedException>(() => converter.Serialize(testCase.Value));
    }

    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void Roundtrip_Value<T>(TestCase<T> testCase)
    {
        YamlConverter<T> converter = GetConverterUnderTest<T>(testCase);

        if (testCase.Value is object value &&
            (value is CSharpHierarchyUnion hierarchy && hierarchy.Value is null ||
             value is CSharpConstructorUnion constructor && constructor.Value is null))
        {
            Assert.Throws<NotSupportedException>(() => converter.Serialize(testCase.Value));
            return;
        }

        string yamlEncoding = converter.Serialize(testCase.Value);

        if (testCase.Value is not null && !providerUnderTest.HasConstructor(testCase))
        {
            Assert.Throws<NotSupportedException>(() => converter.Deserialize(yamlEncoding));
        }
        else
        {
            T? deserializedValue = converter.Deserialize(yamlEncoding);

            if (testCase.IsEquatable)
            {
                Assert.Equal(testCase.Value, deserializedValue);
            }
            else
            {
                if (testCase.IsStack)
                {
                    deserializedValue = converter.Deserialize(converter.Serialize(deserializedValue));
                }

                Assert.Equal(yamlEncoding, converter.Serialize(deserializedValue));
            }
        }
    }

    private YamlConverter<T> GetConverterUnderTest<T>(TestCase<T> testCase) =>
        YamlSerializer.CreateConverter(providerUnderTest.ResolveShape(testCase));
}

public sealed class YamlTests_Reflection() : YamlTests(ReflectionProviderUnderTest.NoEmit);
public sealed class YamlTests_ReflectionEmit() : YamlTests(ReflectionProviderUnderTest.Emit);
public sealed class YamlTests_SourceGen() : YamlTests(SourceGenProviderUnderTest.Default);
