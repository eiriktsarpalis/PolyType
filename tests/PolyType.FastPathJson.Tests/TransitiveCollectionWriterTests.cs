using PolyType.Tests;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Xunit;

namespace PolyType.FastPathJson.Tests;

public static class TransitiveCollectionWriterTests
{
    public static IEnumerable<object[]> PrimitiveValues() =>
        PrimitiveWriterTests.PrimitiveValues().Select(row => new object[] { row[0], new TransitiveCollectionWitness() });

    [Theory]
    [MemberData(nameof(PrimitiveValues))]
    public static void UnregisteredScalarCollectionsPreserveRepresentationAndOptions<T, TWitness>(TestCase<T> testCase, TWitness witness)
        where TWitness : IJsonSerializable<TransitiveCollections<T>>
    {
        _ = witness;
        TransitiveCollections<T> value = new(
            [testCase.Value!, default!],
            [testCase.Value!, default!],
            new() { [0] = testCase.Value!, [1] = default! },
            new() { ["<key>"] = testCase.Value!, ["default"] = default! });

        foreach (JsonWriterOptions options in WriterOptions())
        {
            AssertRoundtrip<TransitiveCollections<T>, TWitness>(value, options);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void UnregisteredCollectionsPreserveNullAndEmptyValues(bool nullValues)
    {
        TransitiveCollections<string> value = nullValues
            ? new(null, null, null, null)
            : new([], [], [], []);

        AssertRoundtrip<TransitiveCollections<string>, TransitiveCollectionWitness>(value, default);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void UnregisteredCollectionsPreserveObjectValues(bool nullValues)
    {
        SimplePoco? item = nullValues ? null : new() { Value = 42 };
        TransitiveCollections<SimplePoco?> value = new(
            [item, null], [item, null], new() { [0] = item }, new() { ["<key>"] = item });

        foreach (JsonWriterOptions options in WriterOptions())
        {
            AssertRoundtrip<TransitiveCollections<SimplePoco?>, TransitiveCollectionWitness>(value, options);
        }
    }

    public static IEnumerable<object[]> NonFiniteValues() =>
        PrimitiveWriterTests.NonFiniteValues().Select(row => new object[] { row[0], new TransitiveCollectionWitness() });

    [Theory]
    [MemberData(nameof(NonFiniteValues))]
    public static void UnregisteredScalarCollectionsRejectNonFiniteValues<T, TWitness>(TestCase<T> testCase, TWitness witness)
        where TWitness : IJsonSerializable<TransitiveCollections<T>>
    {
        _ = witness;
        TransitiveCollections<T>[] values =
        [
            new([testCase.Value!], null, null, null),
            new(null, [testCase.Value!], null, null),
            new(null, null, new() { [0] = testCase.Value! }, null),
            new(null, null, null, new() { ["value"] = testCase.Value! }),
        ];

        foreach (TransitiveCollections<T> value in values)
        {
            foreach (bool skipValidation in new[] { false, true })
            {
                Assert.Throws<ArgumentException>(() =>
                    FastPathJsonSerializer.Serialize<TransitiveCollections<T>, TWitness>(value, new() { SkipValidation = skipValidation }));
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void UnregisteredCollectionsPreserveEnumeratorInvalidation(bool dictionary)
    {
        MutatingCollectionElement first = new(42);
        TransitiveCollections<MutatingCollectionElement> value;
        if (dictionary)
        {
            Dictionary<string, MutatingCollectionElement> items = new() { ["first"] = first };
            first.OnWrite = () => items.Add("second", new(1));
            value = new(null, null, null, items);
        }
        else
        {
            List<MutatingCollectionElement> items = [first];
            first.OnWrite = () => items.Add(new(1));
            value = new(null, items, null, null);
        }

        Assert.Throws<InvalidOperationException>(() =>
            FastPathJsonSerializer.Serialize<TransitiveCollections<MutatingCollectionElement>, TransitiveCollectionWitness>(value));
    }

    private static void AssertRoundtrip<T, TWitness>(T value, JsonWriterOptions options)
        where TWitness : IJsonSerializable<T>
    {
        JsonSerializerOptions referenceOptions = new() { Encoder = options.Encoder, WriteIndented = options.Indented };
        string expected = JsonSerializer.Serialize(value, referenceOptions);
        string actual = FastPathJsonSerializer.Serialize<T, TWitness>(value, options);
        Assert.Equal(expected, actual);
        string roundtrip = JsonSerializer.Serialize(JsonSerializer.Deserialize<T>(expected, referenceOptions), referenceOptions);
        Assert.Equal(roundtrip, FastPathJsonSerializer.Serialize<T, TWitness>(
            FastPathJsonSerializer.Deserialize<T, TWitness>(actual.AsSpan()), options));
        Assert.Equal(roundtrip, FastPathJsonSerializer.Serialize<T, TWitness>(
            FastPathJsonSerializer.Deserialize<T, TWitness>(Encoding.UTF8.GetBytes(actual)), options));
    }

    private static IEnumerable<JsonWriterOptions> WriterOptions()
    {
        foreach (JavaScriptEncoder? encoder in new[]
        {
            null,
            JavaScriptEncoder.Default,
            JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            JavaScriptEncoder.Create(UnicodeRanges.None),
        })
        {
            yield return new() { Encoder = encoder };
            yield return new() { Encoder = encoder, Indented = true };
        }
    }
}

public record TransitiveCollections<T>(
    T[]? Array, List<T>? List, Dictionary<int, T>? IntegerKeys, Dictionary<string, T>? StringKeys);

[GenerateJsonFor<TransitiveCollections<bool>>]
[GenerateJsonFor<TransitiveCollections<sbyte>>]
[GenerateJsonFor<TransitiveCollections<short>>]
[GenerateJsonFor<TransitiveCollections<int>>]
[GenerateJsonFor<TransitiveCollections<long>>]
[GenerateJsonFor<TransitiveCollections<byte>>]
[GenerateJsonFor<TransitiveCollections<ushort>>]
[GenerateJsonFor<TransitiveCollections<uint>>]
[GenerateJsonFor<TransitiveCollections<ulong>>]
[GenerateJsonFor<TransitiveCollections<float>>]
[GenerateJsonFor<TransitiveCollections<double>>]
[GenerateJsonFor<TransitiveCollections<decimal>>]
[GenerateJsonFor<TransitiveCollections<char>>]
[GenerateJsonFor<TransitiveCollections<string>>]
[GenerateJsonFor<TransitiveCollections<byte[]>>]
[GenerateJsonFor<TransitiveCollections<Guid>>]
[GenerateJsonFor<TransitiveCollections<DateTime>>]
[GenerateJsonFor<TransitiveCollections<DateTimeOffset>>]
[GenerateJsonFor<TransitiveCollections<Uri>>]
[GenerateJsonFor<TransitiveCollections<SimplePoco>>]
[GenerateJsonFor<TransitiveCollections<MutatingCollectionElement>>]
public readonly partial struct TransitiveCollectionWitness;
