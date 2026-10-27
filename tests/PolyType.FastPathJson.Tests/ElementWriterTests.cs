using PolyType.Tests;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Xunit;

namespace PolyType.FastPathJson.Tests;

public static class ElementWriterTests
{
    public static IEnumerable<object[]> PrimitiveValues() =>
        PrimitiveWriterTests.PrimitiveValues().Select(row => new object[] { row[0], new ElementWriterWitness() });

    [Theory]
    [MemberData(nameof(PrimitiveValues))]
    public static void RegisteredScalarElementsPreserveRepresentationAndOptions<T, TWitness>(TestCase<T> testCase, TWitness witness)
        where TWitness : IJsonSerializable<T[]>, IJsonSerializable<List<T>>, IJsonSerializable<Dictionary<int, T>>
    {
        _ = witness;
        foreach (JavaScriptEncoder? encoder in new[]
        {
            null,
            JavaScriptEncoder.Default,
            JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            JavaScriptEncoder.Create(UnicodeRanges.None),
        })
        {
            foreach (bool indented in new[] { false, true })
            {
                JsonWriterOptions options = new() { Encoder = encoder, Indented = indented };
                AssertMatchesReference<T[], TWitness>([testCase.Value!, default!], options);
                AssertMatchesReference<List<T>, TWitness>([testCase.Value!, default!], options);
                AssertMatchesReference<Dictionary<int, T>, TWitness>(
                    new() { [0] = testCase.Value!, [1] = default! }, options);
            }
        }
    }

    public static IEnumerable<object[]> NonFiniteValues() =>
        PrimitiveWriterTests.NonFiniteValues().Select(row => new object[] { row[0], new ElementWriterWitness() });

    [Theory]
    [MemberData(nameof(NonFiniteValues))]
    public static void RegisteredScalarElementsRejectNonFiniteValues<T, TWitness>(TestCase<T> testCase, TWitness witness)
        where TWitness : IJsonSerializable<T[]>, IJsonSerializable<List<T>>, IJsonSerializable<Dictionary<int, T>>
    {
        _ = witness;
        foreach (bool skipValidation in new[] { false, true })
        {
            JsonWriterOptions options = new() { SkipValidation = skipValidation };
            Assert.Throws<ArgumentException>(() =>
                FastPathJsonSerializer.Serialize<T[], TWitness>([testCase.Value!], options));
            Assert.Throws<ArgumentException>(() =>
                FastPathJsonSerializer.Serialize<List<T>, TWitness>([testCase.Value!], options));
            Assert.Throws<ArgumentException>(() =>
                FastPathJsonSerializer.Serialize<Dictionary<int, T>, TWitness>(new() { [0] = testCase.Value! }, options));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void RegisteredCollectionsPreserveNullAndEmptyRoots(bool nullValues)
    {
        AssertMatchesReference<int[], ElementWriterWitness>(nullValues ? null : [], default);
        AssertMatchesReference<List<int>, ElementWriterWitness>(nullValues ? null : [], default);
        AssertMatchesReference<Dictionary<int, int>, ElementWriterWitness>(nullValues ? null : [], default);
    }

    private static void AssertMatchesReference<T, TWitness>(T? value, JsonWriterOptions options)
        where TWitness : IJsonSerializable<T>
    {
        JsonSerializerOptions referenceOptions = new() { Encoder = options.Encoder, WriteIndented = options.Indented };
        string expected = JsonSerializer.Serialize(value, referenceOptions);
        string actual = FastPathJsonSerializer.Serialize<T, TWitness>(value, options);
        Assert.Equal(expected, actual);
        string expectedRoundtrip = JsonSerializer.Serialize(JsonSerializer.Deserialize<T>(expected, referenceOptions), referenceOptions);

        Assert.Equal(expectedRoundtrip, FastPathJsonSerializer.Serialize<T, TWitness>(
            FastPathJsonSerializer.Deserialize<T, TWitness>(actual.AsSpan()), options));
        Assert.Equal(expectedRoundtrip, FastPathJsonSerializer.Serialize<T, TWitness>(
            FastPathJsonSerializer.Deserialize<T, TWitness>(Encoding.UTF8.GetBytes(actual)), options));
    }
}

[GenerateJsonFor<int>]
[GenerateJsonFor<bool[]>]
[GenerateJsonFor<sbyte[]>]
[GenerateJsonFor<short[]>]
[GenerateJsonFor<int[]>]
[GenerateJsonFor<long[]>]
[GenerateJsonFor<byte[]>]
[GenerateJsonFor<ushort[]>]
[GenerateJsonFor<uint[]>]
[GenerateJsonFor<ulong[]>]
[GenerateJsonFor<float[]>]
[GenerateJsonFor<double[]>]
[GenerateJsonFor<decimal[]>]
[GenerateJsonFor<char[]>]
[GenerateJsonFor<string[]>]
[GenerateJsonFor<byte[][]>]
[GenerateJsonFor<Guid[]>]
[GenerateJsonFor<DateTime[]>]
[GenerateJsonFor<DateTimeOffset[]>]
[GenerateJsonFor<Uri[]>]
[GenerateJsonFor<List<bool>>]
[GenerateJsonFor<List<sbyte>>]
[GenerateJsonFor<List<short>>]
[GenerateJsonFor<List<int>>]
[GenerateJsonFor<List<long>>]
[GenerateJsonFor<List<byte>>]
[GenerateJsonFor<List<ushort>>]
[GenerateJsonFor<List<uint>>]
[GenerateJsonFor<List<ulong>>]
[GenerateJsonFor<List<float>>]
[GenerateJsonFor<List<double>>]
[GenerateJsonFor<List<decimal>>]
[GenerateJsonFor<List<char>>]
[GenerateJsonFor<List<string>>]
[GenerateJsonFor<List<byte[]>>]
[GenerateJsonFor<List<Guid>>]
[GenerateJsonFor<List<DateTime>>]
[GenerateJsonFor<List<DateTimeOffset>>]
[GenerateJsonFor<List<Uri>>]
[GenerateJsonFor<Dictionary<int, bool>>]
[GenerateJsonFor<Dictionary<int, sbyte>>]
[GenerateJsonFor<Dictionary<int, short>>]
[GenerateJsonFor<Dictionary<int, int>>]
[GenerateJsonFor<Dictionary<int, long>>]
[GenerateJsonFor<Dictionary<int, byte>>]
[GenerateJsonFor<Dictionary<int, ushort>>]
[GenerateJsonFor<Dictionary<int, uint>>]
[GenerateJsonFor<Dictionary<int, ulong>>]
[GenerateJsonFor<Dictionary<int, float>>]
[GenerateJsonFor<Dictionary<int, double>>]
[GenerateJsonFor<Dictionary<int, decimal>>]
[GenerateJsonFor<Dictionary<int, char>>]
[GenerateJsonFor<Dictionary<int, string>>]
[GenerateJsonFor<Dictionary<int, byte[]>>]
[GenerateJsonFor<Dictionary<int, Guid>>]
[GenerateJsonFor<Dictionary<int, DateTime>>]
[GenerateJsonFor<Dictionary<int, DateTimeOffset>>]
[GenerateJsonFor<Dictionary<int, Uri>>]
public readonly partial struct ElementWriterWitness;
