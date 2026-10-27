using PolyType.Tests;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using Xunit;

namespace PolyType.FastPathJson.Tests;

public static class FusedObjectWriterTests
{
    public static IEnumerable<object[]> PrimitiveValues() =>
        PrimitiveWriterTests.PrimitiveValues().Select(row => new object[] { row[0], new FusedObjectWitness() });

    [Theory]
    [MemberData(nameof(PrimitiveValues))]
    public static void GeneratedScalarPropertiesPreserveOutputAndOptions<T, TConverter>(TestCase<T> testCase, TConverter witness)
        where TConverter : IJsonSerializable<FusedObjectPayload<T>>, IJsonSerializable<(T, string)>
    {
        _ = witness;
        FusedObjectPayload<T> value = new(testCase.Value, default);
        (T, string) tuple = (testCase.Value!, "<text>\"\\\n\u00E9");
        foreach (JavaScriptEncoder? encoder in new[]
        {
            JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            null,
            JavaScriptEncoder.Default,
            JavaScriptEncoder.Create(UnicodeRanges.None),
            JavaScriptEncoder.Create(UnicodeRanges.All),
            null,
        })
        {
            foreach (bool indented in new[] { false, true })
            {
                JsonWriterOptions options = new() { Encoder = encoder, Indented = indented };
                JsonSerializerOptions referenceOptions = new() { Encoder = encoder, WriteIndented = indented, IncludeFields = true };
                string expected = JsonSerializer.Serialize(value, referenceOptions);
                Assert.Equal(expected, FastPathJsonSerializer.Serialize<FusedObjectPayload<T>, TConverter>(value, options));
                string expectedRoundtrip = JsonSerializer.Serialize(
                    JsonSerializer.Deserialize<FusedObjectPayload<T>>(expected, referenceOptions), referenceOptions);
                Assert.Equal(expectedRoundtrip, FastPathJsonSerializer.Serialize<FusedObjectPayload<T>, TConverter>(
                    FastPathJsonSerializer.Deserialize<FusedObjectPayload<T>, TConverter>(expected), options));
                string tupleExpected = JsonSerializer.Serialize(tuple, referenceOptions);
                Assert.Equal(tupleExpected, FastPathJsonSerializer.Serialize<(T, string), TConverter>(tuple, options));
                string tupleRoundtrip = JsonSerializer.Serialize(
                    JsonSerializer.Deserialize<(T, string)>(tupleExpected, referenceOptions), referenceOptions);
                Assert.Equal(tupleRoundtrip, FastPathJsonSerializer.Serialize<(T, string), TConverter>(
                    FastPathJsonSerializer.Deserialize<(T, string), TConverter>(tupleExpected), options));
            }
        }
    }

    public static IEnumerable<object[]> NonFiniteValues() =>
        PrimitiveWriterTests.NonFiniteValues().Select(row => new object[] { row[0], new FusedObjectWitness() });

    [Theory]
    [MemberData(nameof(NonFiniteValues))]
    public static void FusedNumericPropertiesRejectNonFiniteValues<T, TConverter>(TestCase<T> testCase, TConverter witness)
        where TConverter : IJsonSerializable<FusedObjectPayload<T>>, IJsonSerializable<(T, string)>
    {
        _ = witness;
        foreach (bool skipValidation in new[] { false, true })
        {
            JsonWriterOptions options = new() { SkipValidation = skipValidation };
            Assert.Throws<ArgumentException>(() => FastPathJsonSerializer.Serialize<FusedObjectPayload<T>, TConverter>(
                new(testCase.Value, default), options));
            Assert.Throws<ArgumentException>(() =>
                FastPathJsonSerializer.Serialize<(T, string), TConverter>((testCase.Value!, "text"), options));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void PropertyFusionPreservesGetterOrderAndNullGuards(bool nullString)
    {
        List<string> getters = [];
        FusedGetterPayload value = new() { OnGet = getters.Add, Text = nullString ? null : "text" };
        if (nullString)
        {
            JsonException exception = Assert.Throws<JsonException>(() => FastPathJsonSerializer.Serialize(value));
            Assert.Equal("The property 'String' cannot contain null.", exception.Message);
            Assert.Equal([nameof(FusedGetterPayload.Bool), nameof(FusedGetterPayload.String)], getters);
        }
        else
        {
            Assert.Equal("""{"Bool":true,"String":"text","Last":42}""", FastPathJsonSerializer.Serialize(value));
            Assert.Equal([nameof(FusedGetterPayload.Bool), nameof(FusedGetterPayload.String), nameof(FusedGetterPayload.Last)], getters);
        }
    }
}

public record FusedObjectPayload<T>(
    [property: JsonPropertyName("<value>\"\\\n\u00E9")] T? Value,
    [property: JsonPropertyName("default")] T? Default);

[GenerateJson]
public partial class FusedGetterPayload
{
    [JsonIgnore]
    public Action<string>? OnGet { get; set; }

    [JsonIgnore]
    public string? Text { get; set; }

    [JsonPropertyOrder(0)]
    public bool Bool
    {
        get
        {
            OnGet?.Invoke(nameof(Bool));

            return true;
        }
    }

    [JsonPropertyOrder(1)]
    public string String
    {
        get
        {
            OnGet?.Invoke(nameof(String));

            return Text!;
        }
    }

    [JsonPropertyOrder(2)]
    public int Last
    {
        get
        {
            OnGet?.Invoke(nameof(Last));

            return 42;
        }
    }
}

[GenerateJsonFor<FusedObjectPayload<bool>>]
[GenerateJsonFor<FusedObjectPayload<sbyte>>]
[GenerateJsonFor<FusedObjectPayload<short>>]
[GenerateJsonFor<FusedObjectPayload<int>>]
[GenerateJsonFor<FusedObjectPayload<long>>]
[GenerateJsonFor<FusedObjectPayload<byte>>]
[GenerateJsonFor<FusedObjectPayload<ushort>>]
[GenerateJsonFor<FusedObjectPayload<uint>>]
[GenerateJsonFor<FusedObjectPayload<ulong>>]
[GenerateJsonFor<FusedObjectPayload<float>>]
[GenerateJsonFor<FusedObjectPayload<double>>]
[GenerateJsonFor<FusedObjectPayload<decimal>>]
[GenerateJsonFor<FusedObjectPayload<char>>]
[GenerateJsonFor<FusedObjectPayload<string>>]
[GenerateJsonFor<FusedObjectPayload<byte[]>>]
[GenerateJsonFor<FusedObjectPayload<Guid>>]
[GenerateJsonFor<FusedObjectPayload<DateTime>>]
[GenerateJsonFor<FusedObjectPayload<DateTimeOffset>>]
[GenerateJsonFor<FusedObjectPayload<Uri>>]
[GenerateJsonFor<(bool, string)>]
[GenerateJsonFor<(sbyte, string)>]
[GenerateJsonFor<(short, string)>]
[GenerateJsonFor<(int, string)>]
[GenerateJsonFor<(long, string)>]
[GenerateJsonFor<(byte, string)>]
[GenerateJsonFor<(ushort, string)>]
[GenerateJsonFor<(uint, string)>]
[GenerateJsonFor<(ulong, string)>]
[GenerateJsonFor<(float, string)>]
[GenerateJsonFor<(double, string)>]
[GenerateJsonFor<(decimal, string)>]
[GenerateJsonFor<(char, string)>]
[GenerateJsonFor<(string, string)>]
[GenerateJsonFor<(byte[], string)>]
[GenerateJsonFor<(Guid, string)>]
[GenerateJsonFor<(DateTime, string)>]
[GenerateJsonFor<(DateTimeOffset, string)>]
[GenerateJsonFor<(Uri, string)>]
public partial class FusedObjectWitness;
