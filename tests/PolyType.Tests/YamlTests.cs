using System.Collections.Immutable;
using System.Numerics;
using PolyType.Examples.YamlSerializer;
using Xunit;

namespace PolyType.Tests;

public abstract class YamlTests(ProviderUnderTest providerUnderTest)
{
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
        yield return [TestCase.Create((float)0.2, p), "0.20000000298023224"];
        yield return [TestCase.Create(decimal.MaxValue, p), "79228162514264337593543950335"];
        yield return [TestCase.Create('c', p), "c"];
        yield return [TestCase.Create((string?)null, p), "null"];
        yield return [TestCase.Create("", p), "''"];
        yield return [TestCase.Create("Hello World", p), "Hello World"];
        yield return [TestCase.Create(Guid.Empty, p), "00000000-0000-0000-0000-000000000000"];
        yield return [TestCase.Create(new SimpleRecord(value: 42)), "value: 42"];
#if NET8_0_OR_GREATER
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
    }

    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void Roundtrip_Value<T>(TestCase<T> testCase)
    {
        YamlConverter<T> converter = GetConverterUnderTest<T>(testCase);

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
