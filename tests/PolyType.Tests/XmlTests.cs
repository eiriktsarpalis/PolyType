using System.Collections.Immutable;
using System.Numerics;
using System.Xml;
using PolyType.Examples.XmlSerializer;
using PolyType.Tests.FSharp;
using Xunit;

namespace PolyType.Tests;

public abstract class XmlTests(ProviderUnderTest providerUnderTest)
{
    private static readonly XmlWriterSettings s_writerSettings = new()
    {
        ConformanceLevel = ConformanceLevel.Fragment,
        Indent = false
    };

    [Theory]
    [MemberData(nameof(GetValuesAndExpectedEncoding))]
    public void ReturnsExpectedEncoding<T>(TestCase<T> testCase, string expectedEncoding)
    {
        XmlConverter<T> converter = GetConverterUnderTest(testCase);

        string xml = converter.Serialize(testCase.Value, s_writerSettings);
        Assert.Equal(expectedEncoding, xml);
    }

    [Theory]
    [MemberData(nameof(GetValuesAndExpectedEncoding))]
    public void ExpectedEncodingDeserializedToValue<T>(TestCase<T> testCase, string expectedEncoding)
    {
        XmlConverter<T> converter = GetConverterUnderTest(testCase);

        T? result = converter.Deserialize(expectedEncoding);
        if (testCase.IsEquatable)
        {
            Assert.Equal(testCase.Value, result);
        }
        else
        {
            Assert.Equal(expectedEncoding, converter.Serialize(result, s_writerSettings));
        }
    }

    public static IEnumerable<object?[]> GetValuesAndExpectedEncoding()
    {
        Witness p = new();
        yield return [TestCase.Create((object?)null, p), """<value nil="true" />"""];
        yield return [TestCase.Create(false, p), "<value>false</value>"];
        yield return [TestCase.Create(true, p), "<value>true</value>"];
        yield return [TestCase.Create(42, p), "<value>42</value>"];
        yield return [TestCase.Create(-7001, p), "<value>-7001</value>"];
        yield return [TestCase.Create((byte)255, p), "<value>255</value>"];
        yield return [TestCase.Create(int.MaxValue, p), "<value>2147483647</value>"];
        yield return [TestCase.Create(int.MinValue, p), "<value>-2147483648</value>"];
        yield return [TestCase.Create(long.MaxValue, p), "<value>9223372036854775807</value>"];
        yield return [TestCase.Create(long.MinValue, p), "<value>-9223372036854775808</value>"];
        yield return [TestCase.Create((BigInteger)long.MaxValue, p), "<value>9223372036854775807</value>"];
        yield return [TestCase.Create((float)0.2, p), "<value>0.2</value>"];
        yield return [TestCase.Create(decimal.MaxValue, p), "<value>79228162514264337593543950335</value>"];
        yield return [TestCase.Create((int[])[1, 2, 3], p), "<value><element>1</element><element>2</element><element>3</element></value>"];
        yield return [TestCase.Create((List<int>)[1, 2, 3], p), "<value><element>1</element><element>2</element><element>3</element></value>"];
        yield return [TestCase.Create(new Dictionary<string, string> { ["key1"] = "value", ["key2"] = "value" }, p), "<value><entry><key>key1</key><value>value</value></entry><entry><key>key2</key><value>value</value></entry></value>"];
        yield return [TestCase.Create(ImmutableSortedDictionary.CreateRange<string, string>([new("key1", "value"), new("key2", "value")]), p), "<value><entry><key>key1</key><value>value</value></entry><entry><key>key2</key><value>value</value></entry></value>"];
        yield return [TestCase.Create((byte[])[1, 2, 3], p), "<value>AQID</value>"];
        yield return [TestCase.Create('c', p), "<value>c</value>"];
        yield return [TestCase.Create((string?)null, p), """<value nil="true" />"""];
        yield return [TestCase.Create("", p), "<value></value>"];
        yield return [TestCase.Create("Hello, World!", p), "<value>Hello, World!</value>"];
        yield return [TestCase.Create(Guid.Empty, p), "<value>00000000-0000-0000-0000-000000000000</value>"];
        yield return [TestCase.Create(TimeSpan.MinValue, p), "<value>-10675199.02:48:05.4775808</value>"];
        yield return [TestCase.Create(DateTimeOffset.MinValue, p), "<value>0001-01-01T00:00:00Z</value>"];
#if NET8_0_OR_GREATER
        yield return [TestCase.Create(Int128.MaxValue, p), "<value>170141183460469231731687303715884105727</value>"];
        yield return [TestCase.Create((Half)1, p), "<value>1</value>"];
        yield return [TestCase.Create(DateOnly.MaxValue, p), "<value>9999-12-31</value>"];
        yield return [TestCase.Create(TimeOnly.MaxValue, p), "<value>23:59:59.9999999</value>"];
#endif
        yield return [TestCase.Create(new SimpleRecord(value: 42)), "<value><value>42</value></value>"];
        yield return [TestCase.Create(new BaseClass { X = 42 }), "<value><X>42</X></value>"];
        yield return [TestCase.Create(new PolymorphicClass(42)), """<value type="PolymorphicClass"><Int>42</Int></value>"""];
        yield return [TestCase.Create<PolymorphicClass>(new PolymorphicClass.DerivedClass(42, "str")), """<value type="DerivedClass"><String>str</String><Int>42</Int></value>"""];
        yield return [TestCase.Create<Tree>(new Tree.Leaf()), """<value type="leaf" />"""];
        yield return [TestCase.Create<Tree>(new Tree.Node(42, new Tree.Leaf(), new Tree.Leaf())), """<value type="node"><Value>42</Value><Left type="leaf" /><Right type="leaf" /></value>"""];
        yield return [TestCase.Create(FSharpUnion.NewC(42), p), """<value type="C"><foo>42</foo></value>"""];
        yield return [TestCase.Create(FSharpUnion.B, p), """<value type="B" />"""];
        yield return [TestCase.Create(FSharpStructUnion.NewC(42), p), """<value type="C"><foo>42</foo></value>"""];
        yield return [TestCase.Create<ObjectSurrogateHierarchy>(new ObjectSurrogateHierarchy.Text("42")), """<value type="text">42</value>"""];
        yield return [TestCase.Create(new CSharpScalarUnion(42)), """<value type="Int32">42</value>"""];
        yield return [TestCase.Create(default(CSharpScalarUnion)), """<value type="String" nil="true" />"""];
        yield return [TestCase.Create(new CSharpArrayUnion(new int[] { 1, 2 })), """<value type="Array_Int32"><element>1</element><element>2</element></value>"""];
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClassUnionNullPayloadUsesReferenceUnionNullSemantics(bool nullReference)
    {
        var converter = GetConverterUnderTest(TestCase.Create(new CSharpClassUnion(42)));
        CSharpClassUnion? value = nullReference ? null : new((string?)null);
        string xml = converter.Serialize(value, s_writerSettings);
        Assert.Equal(nullReference ? """<value nil="true" />""" : """<value type="String" nil="true" />""", xml);

        CSharpClassUnion? result = converter.Deserialize(xml);
        Assert.Null(result);
        Assert.Equal("""<value nil="true" />""", converter.Serialize(result, s_writerSettings));
    }

    [Fact]
    public void DirectlyNestedUnionsCannotShareAnXmlTypeAttribute()
    {
        var testCase = TestCase.Create(new CSharpRecursiveUnion(new CSharpRecursiveUnion(true)));
        var converter = GetConverterUnderTest(testCase);
        Assert.Throws<XmlException>(() => converter.Serialize(testCase.Value, s_writerSettings));
    }

    [Theory]
    [MemberData(nameof(GetRoundtrippableCases))]
    public void Roundtrip_Value<T>(TestCase<T> testCase)
    {
        XmlConverter<T> converter = GetConverterUnderTest<T>(testCase);

        string xmlEncoding = converter.Serialize(testCase.Value);

        if (testCase.Value is not null && !providerUnderTest.HasConstructor(testCase))
        {
            Assert.Throws<NotSupportedException>(() => converter.Deserialize(xmlEncoding));
        }
        else
        {
            T? deserializedValue = converter.Deserialize(xmlEncoding);
            if (default(T) is null && testCase.IsUnion &&
                System.Xml.Linq.XElement.Parse(xmlEncoding).Attribute("nil")?.Value == "true")
            {
                Assert.Null(deserializedValue);
                return;
            }

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

                Assert.Equal(xmlEncoding, converter.Serialize(deserializedValue));
            }
        }
    }

    public static IEnumerable<object[]> GetRoundtrippableCases()
    {
        foreach (object[] row in TestTypes.GetTestCases())
        {
            Type type = ((ITestCase)row[0]).Type;
            if (type == typeof(CSharpRecursiveUnion) ||
                type == typeof(CSharpMutualUnionA) ||
                type == typeof(CSharpMutualUnionB) ||
                type == typeof(CSharpOptionalUnion) ||
                type == typeof(CSharpGenericUnion<CanonicalUnionTree, bool>))
            {
                // Leaf and structurally nested values are covered by the exact-encoding tests;
                // directly nested union payloads cannot share this format's type attribute.
                continue;
            }
            yield return row;
        }
    }

    private XmlConverter<T> GetConverterUnderTest<T>(TestCase<T> testCase) =>
        XmlSerializer.CreateConverter(providerUnderTest.ResolveShape(testCase));
}

public sealed class XmlTests_Reflection() : XmlTests(ReflectionProviderUnderTest.NoEmit);
public sealed class XmlTests_ReflectionEmit() : XmlTests(ReflectionProviderUnderTest.Emit);
public sealed class XmlTests_SourceGen() : XmlTests(SourceGenProviderUnderTest.Default);