using PolyType.FastPathJson.Converters;
using PolyType.Tests;
using System.Buffers;
using System.Numerics;
using System.Text;
using System.Text.Json;
using Xunit;

namespace PolyType.FastPathJson.Tests;

public static class SerializerEdgeCaseTests
{
    public static IEnumerable<object[]> InvalidScalars()
    {
        yield return Invalid<char, CharConverter>("42", typeof(InvalidOperationException));
        yield return Invalid<char, CharConverter>("null", typeof(InvalidOperationException));
        yield return Invalid<char, CharConverter>("\"\"", typeof(InvalidOperationException));
        yield return Invalid<char, CharConverter>("\"1234567\"", typeof(InvalidOperationException));
        yield return Invalid<TimeSpan, TimeSpanConverter>("true", typeof(InvalidOperationException));
        yield return Invalid<DateOnly, DateOnlyConverter>("true", typeof(InvalidOperationException));
        yield return Invalid<TimeOnly, TimeOnlyConverter>("true", typeof(InvalidOperationException));
        yield return Invalid<Version, VersionConverter>("42", typeof(InvalidOperationException));
        yield return Invalid<Int128, Int128Converter>("true", typeof(InvalidOperationException));
        yield return Invalid<UInt128, UInt128Converter>("true", typeof(InvalidOperationException));
        yield return Invalid<Half, HalfConverter>("true", typeof(InvalidOperationException));
        yield return Invalid<BigInteger, BigIntegerConverter>("\"42\"", typeof(JsonException));
        yield return Invalid<Rune, RuneConverter>("null", typeof(JsonException));
    }

    [Theory]
    [MemberData(nameof(InvalidScalars))]
    public static void InvalidScalarTokensAreRejected(Action read, Type exceptionType) =>
        Assert.Throws(exceptionType, read);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void LongAndSegmentedScalarsPreserveValues(bool segmented)
    {
        string key = string.Concat(Enumerable.Repeat("\\u0030", 64));
        string json = "{\"" + key + "\":42}";
        Assert.Equal(42, Read<Dictionary<int, int>, DictionaryConverter<int, int, Int32Converter, Int32Converter>>(json, segmented)![0]);
        Assert.Equal(42, Read<Dictionary<Int128, int>, DictionaryConverter<Int128, int, Int128Converter, Int32Converter>>(json, segmented)![0]);
        Assert.Equal(42, Read<Dictionary<Half, int>, DictionaryConverter<Half, int, HalfConverter, Int32Converter>>(json, segmented)![(Half)0]);
        Assert.Equal((Half)0, Read<Half, HalfConverter>("0." + new string('0', 300), segmented));

        AssertKeyRoundtrip<char, CharConverter>('A', segmented);
        AssertKeyRoundtrip<char, CharConverter>('"', segmented);
        AssertKeyRoundtrip<char, CharConverter>('\u00E9', segmented);
        AssertKeyRoundtrip<Guid, GuidConverter>(new("12345678-1234-5678-9abc-123456789abc"), segmented);
        AssertKeyRoundtrip<DateTime, DateTimeConverter>(new(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc), segmented);
        AssertKeyRoundtrip<DateTimeOffset, DateTimeOffsetConverter>(new(2026, 10, 27, 12, 0, 0, TimeSpan.FromHours(2)), segmented);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void RuntimeCollectionWitnessesPreserveNullAndNonNullValues(bool nullValues)
    {
        AssertRoundtrip<int?, NullableConverter<int, Int32Converter>>(nullValues ? null : 42, nullValues ? "null" : "42");
        AssertRoundtrip<int[], ArrayConverter<int, Int32Converter>>(nullValues ? null : [1, 2], nullValues ? "null" : "[1,2]");
        AssertRoundtrip<List<int>, ListConverter<int, Int32Converter>>(nullValues ? null : [1, 2], nullValues ? "null" : "[1,2]");
        AssertRoundtrip<Dictionary<string, int>, DictionaryConverter<string, int, StringConverter, Int32Converter>>(
            nullValues ? null : new() { ["value"] = 42 }, nullValues ? "null" : """{"value":42}""");

        const string json = """{"$type":{"ignored":[1,2]},"value":42}""";
        var value = FastPathJsonSerializer.Deserialize<Dictionary<string, int>,
            DictionaryConverter<string, int, StringConverter, Int32Converter>>(json);
        Assert.NotNull(value);
        Assert.Single(value);
        Assert.Equal(42, value["value"]);
    }

    [Theory]
    [InlineData("[", false)]
    [InlineData("[1,", false)]
    [InlineData("42", false)]
    [InlineData("{", true)]
    [InlineData("{\"value\"", true)]
    [InlineData("{\"value\":", true)]
    [InlineData("{\"value\":42,", true)]
    [InlineData("[]", true)]
    public static void IncompleteAndInvalidCollectionsAreRejected(string json, bool dictionary)
    {
        if (dictionary)
        {
            Assert.Throws<JsonException>(() => Read<Dictionary<string, int>,
                DictionaryConverter<string, int, StringConverter, Int32Converter>>(json, segmented: true, isFinalBlock: false));
        }
        else
        {
            Assert.Throws<JsonException>(() =>
                Read<int[], ArrayConverter<int, Int32Converter>>(json, segmented: true, isFinalBlock: false));
            Assert.Throws<JsonException>(() =>
                Read<List<int>, ListConverter<int, Int32Converter>>(json, segmented: true, isFinalBlock: false));
        }
    }

    public static IEnumerable<object[]> UnsupportedKeys()
    {
        yield return Unsupported<SimplePoco, TestCasesWitness>(new() { Value = 42 });
        yield return Unsupported<BigInteger, BigIntegerConverter>(new(42));
        yield return Unsupported<Rune, RuneConverter>(new('A'));
        yield return Unsupported<byte[], Array_ByteConverter>([1, 2]);
    }

    [Theory]
    [MemberData(nameof(UnsupportedKeys))]
    public static void UnsupportedDictionaryKeysAreRejected(Action write, Action read)
    {
        Assert.Throws<NotSupportedException>(write);
        Assert.Throws<NotSupportedException>(read);
    }

    private static object[] Invalid<T, TWitness>(string json, Type exceptionType)
        where TWitness : IJsonSerializable<T> =>
        [(Action)(() => FastPathJsonSerializer.Deserialize<T, TWitness>(json)), exceptionType];

    private static object[] Unsupported<T, TWitness>(T key)
        where T : notnull
        where TWitness : IJsonSerializable<T> =>
        [
            (Action)(() => FastPathJsonSerializer.Serialize<Dictionary<T, int>,
                DictionaryConverter<T, int, TWitness, Int32Converter>>(new() { [key] = 42 })),
            (Action)(() => FastPathJsonSerializer.Deserialize<Dictionary<T, int>,
                DictionaryConverter<T, int, TWitness, Int32Converter>>("""{"key":42}""")),
        ];

    private static void AssertRoundtrip<T, TWitness>(T? value, string expected)
        where TWitness : IJsonSerializable<T>
    {
        Assert.Equal(expected, FastPathJsonSerializer.Serialize<T, TWitness>(value));
        Assert.Equal(expected, FastPathJsonSerializer.Serialize<T, TWitness>(
            FastPathJsonSerializer.Deserialize<T, TWitness>(expected)));
    }

    private static void AssertKeyRoundtrip<T, TWitness>(T key, bool segmented)
        where T : notnull
        where TWitness : IJsonSerializable<T>
    {
        Dictionary<T, int> value = new() { [key] = 42 };
        string json = FastPathJsonSerializer.Serialize<Dictionary<T, int>,
            DictionaryConverter<T, int, TWitness, Int32Converter>>(value);
        Assert.Equal(JsonSerializer.Serialize(value), json);
        var restored = Read<Dictionary<T, int>, DictionaryConverter<T, int, TWitness, Int32Converter>>(json, segmented);
        Assert.NotNull(restored);
        Assert.Equal(42, restored[key]);
    }

    private static T? Read<T, TWitness>(string json, bool segmented, bool isFinalBlock = true)
        where TWitness : IJsonSerializable<T>
    {
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        if (!segmented)
        {
            Utf8JsonReader reader = new(bytes, isFinalBlock, state: default);
            return FastPathJsonSerializer.Deserialize<T, TWitness>(ref reader);
        }

        Segment first = new(bytes.AsMemory(0, 1));
        Segment last = first;
        for (int i = 1; i < bytes.Length; i++)
        {
            last = last.Append(bytes.AsMemory(i, 1));
        }

        Utf8JsonReader segmentedReader = new(new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length), isFinalBlock, state: default);

        return FastPathJsonSerializer.Deserialize<T, TWitness>(ref segmentedReader);
    }

    private sealed class Segment : ReadOnlySequenceSegment<byte>
    {
        public Segment(ReadOnlyMemory<byte> memory) => Memory = memory;

        public Segment Append(ReadOnlyMemory<byte> memory)
        {
            Segment next = new(memory) { RunningIndex = RunningIndex + Memory.Length };
            Next = next;

            return next;
        }
    }
}
