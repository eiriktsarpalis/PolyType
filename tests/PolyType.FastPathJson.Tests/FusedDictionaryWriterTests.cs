using PolyType.Tests;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Xunit;

namespace PolyType.FastPathJson.Tests;

public static class FusedDictionaryWriterTests
{
    public static IEnumerable<object[]> PrimitiveValues() =>
        PrimitiveWriterTests.PrimitiveValues().Select(row => new object[] { row[0], new FusedDictionaryWitness() });

    [Theory]
    [MemberData(nameof(PrimitiveValues))]
    public static void GeneratedScalarEntriesPreserveOutputAndOptions<T, TConverter>(TestCase<T> testCase, TConverter witness)
        where TConverter : IJsonSerializable<Dictionary<string, T>>, IJsonSerializable<SortedDictionary<string, T>>
    {
        _ = witness;
        Dictionary<string, T> dictionary = new()
        {
            ["<key>\"\\\n\u00E9"] = testCase.Value!,
            ["default"] = default!,
            [""] = testCase.Value!,
        };
        SortedDictionary<string, T> sorted = new(dictionary);
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
                JsonSerializerOptions referenceOptions = new() { Encoder = encoder, WriteIndented = indented };
                string expected = JsonSerializer.Serialize(dictionary, referenceOptions);
                Assert.Equal(expected, FastPathJsonSerializer.Serialize<Dictionary<string, T>, TConverter>(dictionary, options));
                string expectedRoundtrip = JsonSerializer.Serialize(
                    JsonSerializer.Deserialize<Dictionary<string, T>>(expected), referenceOptions);
                Assert.Equal(expectedRoundtrip, FastPathJsonSerializer.Serialize<Dictionary<string, T>, TConverter>(
                    FastPathJsonSerializer.Deserialize<Dictionary<string, T>, TConverter>(expected), options));
                string sortedExpected = JsonSerializer.Serialize(sorted, referenceOptions);
                Assert.Equal(sortedExpected, FastPathJsonSerializer.Serialize<SortedDictionary<string, T>, TConverter>(sorted, options));
                string sortedExpectedRoundtrip = JsonSerializer.Serialize(
                    JsonSerializer.Deserialize<SortedDictionary<string, T>>(sortedExpected), referenceOptions);
                Assert.Equal(sortedExpectedRoundtrip, FastPathJsonSerializer.Serialize<SortedDictionary<string, T>, TConverter>(
                    FastPathJsonSerializer.Deserialize<SortedDictionary<string, T>, TConverter>(sortedExpected), options));
            }
        }
    }

    public static IEnumerable<object[]> NonFiniteValues() =>
        PrimitiveWriterTests.NonFiniteValues().Select(row => new object[] { row[0], new FusedDictionaryWitness() });

    [Theory]
    [MemberData(nameof(NonFiniteValues))]
    public static void FusedNumericEntriesRejectNonFiniteValues<T, TConverter>(TestCase<T> testCase, TConverter witness)
        where TConverter : IJsonSerializable<Dictionary<string, T>>, IJsonSerializable<SortedDictionary<string, T>>
    {
        _ = witness;
        Dictionary<string, T> dictionary = new() { ["value"] = testCase.Value! };
        SortedDictionary<string, T> sorted = new(dictionary);
        foreach (bool skipValidation in new[] { false, true })
        {
            JsonWriterOptions options = new() { SkipValidation = skipValidation };
            Assert.Throws<ArgumentException>(() =>
                FastPathJsonSerializer.Serialize<Dictionary<string, T>, TConverter>(dictionary, options));
            Assert.Throws<ArgumentException>(() =>
                FastPathJsonSerializer.Serialize<SortedDictionary<string, T>, TConverter>(sorted, options));
        }
    }
}

[GenerateJsonFor<Dictionary<string, bool>>]
[GenerateJsonFor<Dictionary<string, sbyte>>]
[GenerateJsonFor<Dictionary<string, short>>]
[GenerateJsonFor<Dictionary<string, int>>]
[GenerateJsonFor<Dictionary<string, long>>]
[GenerateJsonFor<Dictionary<string, byte>>]
[GenerateJsonFor<Dictionary<string, ushort>>]
[GenerateJsonFor<Dictionary<string, uint>>]
[GenerateJsonFor<Dictionary<string, ulong>>]
[GenerateJsonFor<Dictionary<string, float>>]
[GenerateJsonFor<Dictionary<string, double>>]
[GenerateJsonFor<Dictionary<string, decimal>>]
[GenerateJsonFor<Dictionary<string, char>>]
[GenerateJsonFor<Dictionary<string, string>>]
[GenerateJsonFor<Dictionary<string, byte[]>>]
[GenerateJsonFor<Dictionary<string, Guid>>]
[GenerateJsonFor<Dictionary<string, DateTime>>]
[GenerateJsonFor<Dictionary<string, DateTimeOffset>>]
[GenerateJsonFor<Dictionary<string, Uri>>]
[GenerateJsonFor<SortedDictionary<string, bool>>]
[GenerateJsonFor<SortedDictionary<string, sbyte>>]
[GenerateJsonFor<SortedDictionary<string, short>>]
[GenerateJsonFor<SortedDictionary<string, int>>]
[GenerateJsonFor<SortedDictionary<string, long>>]
[GenerateJsonFor<SortedDictionary<string, byte>>]
[GenerateJsonFor<SortedDictionary<string, ushort>>]
[GenerateJsonFor<SortedDictionary<string, uint>>]
[GenerateJsonFor<SortedDictionary<string, ulong>>]
[GenerateJsonFor<SortedDictionary<string, float>>]
[GenerateJsonFor<SortedDictionary<string, double>>]
[GenerateJsonFor<SortedDictionary<string, decimal>>]
[GenerateJsonFor<SortedDictionary<string, char>>]
[GenerateJsonFor<SortedDictionary<string, string>>]
[GenerateJsonFor<SortedDictionary<string, byte[]>>]
[GenerateJsonFor<SortedDictionary<string, Guid>>]
[GenerateJsonFor<SortedDictionary<string, DateTime>>]
[GenerateJsonFor<SortedDictionary<string, DateTimeOffset>>]
[GenerateJsonFor<SortedDictionary<string, Uri>>]
public partial class FusedDictionaryWitness;
