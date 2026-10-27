using PolyType.FastPathJson.Converters;
using PolyType.Tests;
using System.Buffers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Xunit;

namespace PolyType.FastPathJson.Tests;

public static class CallerOwnedJsonTests
{
    [Theory]
    [MemberData(nameof(SerializerTests.GetSerializationBaselines), MemberType = typeof(SerializerTests))]
    public static void ExplicitWitnessOverloadsComposeValues<T, TWitness>(TestCase<T> testCase, TWitness witness, string expectedJson)
        where TWitness : IJsonSerializable<T>
    {
        _ = witness;
        ArrayBufferWriter<byte> buffer = new();
        using Utf8JsonWriter writer = new(buffer);
        writer.WriteStartArray();
        FastPathJsonSerializer.Serialize<T, TWitness>(writer, testCase.Value);
        Assert.Equal("[" + expectedJson, Encoding.UTF8.GetString(buffer.WrittenSpan));
        Assert.Equal(0, writer.BytesPending);

        FastPathJsonSerializer.Serialize<int, Int32Converter>(writer, 42);
        writer.WriteEndArray();
        writer.Flush();
        Assert.Equal($"[{expectedJson},42]", Encoding.UTF8.GetString(buffer.WrittenSpan));

        Utf8JsonReader reader = new(buffer.WrittenSpan);
        Assert.True(reader.Read());
        Assert.True(reader.Read());
        T? value = FastPathJsonSerializer.Deserialize<T, TWitness>(ref reader);
        Assert.Equal(1L + Encoding.UTF8.GetByteCount(expectedJson), reader.BytesConsumed);
        Assert.Equal(expectedJson, FastPathJsonSerializer.Serialize<T, TWitness>(value));
        Assert.True(reader.Read());
        Assert.Equal(42, FastPathJsonSerializer.Deserialize<int, Int32Converter>(ref reader));
        Assert.Equal(JsonTokenType.Number, reader.TokenType);
        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.EndArray, reader.TokenType);
        Assert.False(reader.Read());
    }

    [Theory]
    [InlineData(JsonTokenType.None, false)]
    [InlineData(JsonTokenType.None, true)]
    [InlineData(JsonTokenType.StartObject, false)]
    [InlineData(JsonTokenType.StartObject, true)]
    [InlineData(JsonTokenType.PropertyName, false)]
    [InlineData(JsonTokenType.PropertyName, true)]
    public static void SelfSerializableOverloadsPreserveOptionsAndReaderPosition(JsonTokenType position, bool indented)
    {
        Receipt value = new(42, "<customer>") { Lines = [new("<book>", 12.50m)] };
        foreach (JavaScriptEncoder? encoder in new[]
        {
            null, JavaScriptEncoder.Default, JavaScriptEncoder.UnsafeRelaxedJsonEscaping, JavaScriptEncoder.Create(UnicodeRanges.None),
        })
        {
            JsonWriterOptions options = new() { Encoder = encoder, Indented = indented };
            string expected = JsonSerializer.Serialize(value, new JsonSerializerOptions { Encoder = encoder, WriteIndented = indented });
            ArrayBufferWriter<byte> buffer = new();
            using Utf8JsonWriter writer = new(buffer, options);
            FastPathJsonSerializer.Serialize(writer, value);
            Assert.Equal(expected, Encoding.UTF8.GetString(buffer.WrittenSpan));
            Assert.Equal(0, writer.BytesPending);

            buffer.ResetWrittenCount();
            writer.Reset();
            FastPathJsonSerializer.Serialize<Receipt>(writer, null);
            Assert.Equal("null", Encoding.UTF8.GetString(buffer.WrittenSpan));

            string prefix = position is JsonTokenType.PropertyName ? "{\"value\":" : "";
            string json = prefix + expected + (prefix.Length > 0 ? "}" : "");
            Utf8JsonReader reader = new(Encoding.UTF8.GetBytes(json));
            if (position is not JsonTokenType.None)
            {
                Assert.True(reader.Read());
                if (position is JsonTokenType.PropertyName)
                {
                    Assert.True(reader.Read());
                }
            }

            Receipt? restored = FastPathJsonSerializer.Deserialize<Receipt>(ref reader);
            Assert.Equal(expected, FastPathJsonSerializer.Serialize(restored, options));
            Assert.Equal(JsonTokenType.EndObject, reader.TokenType);
            Assert.Equal(Encoding.UTF8.GetByteCount(prefix + expected), reader.BytesConsumed);
            if (position is JsonTokenType.PropertyName)
            {
                Assert.True(reader.Read());
                Assert.Equal(JsonTokenType.EndObject, reader.TokenType);
            }

            Assert.False(reader.Read());
            Utf8JsonReader nullReader = new("null"u8);
            Assert.Null(FastPathJsonSerializer.Deserialize<Receipt>(ref nullReader));
            Assert.Equal(JsonTokenType.Null, nullReader.TokenType);
        }
    }

    [Theory]
    [MemberData(nameof(SerializerTests.GetSerializationBaselines), MemberType = typeof(SerializerTests))]
    public static void ReaderOverloadPreservesSegmentedInput<T, TWitness>(TestCase<T> testCase, TWitness witness, string expectedJson)
        where TWitness : IJsonSerializable<T>
    {
        _ = testCase;
        _ = witness;
        byte[] bytes = Encoding.UTF8.GetBytes(expectedJson);
        int split = bytes.Length / 2;
        Segment first = new(bytes.AsMemory(0, split));
        Segment last = first.Append(bytes.AsMemory(split));
        Utf8JsonReader reader = new(new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length));

        T? value = FastPathJsonSerializer.Deserialize<T, TWitness>(ref reader);
        Assert.Equal(expectedJson, FastPathJsonSerializer.Serialize<T, TWitness>(value));
        Assert.Equal(bytes.Length, reader.BytesConsumed);
        Assert.False(reader.Read());
    }

    [Theory]
    [InlineData("", true, null)]
    [InlineData(" \r\n\t", true, null)]
    [InlineData("", false, "Expected a JSON value.")]
    [InlineData(" \r\n\t", false, "Expected a JSON value.")]
    [InlineData("{", false, "Incomplete JSON value.")]
    [InlineData("{\"Number\":42,", false, "Incomplete JSON value.")]
    public static void ReaderOverloadsRejectMissingOrIncompleteValues(string json, bool isFinalBlock, string? expectedMessage)
    {
        JsonException selfError = Assert.ThrowsAny<JsonException>(() =>
        {
            Utf8JsonReader reader = new(Encoding.UTF8.GetBytes(json), isFinalBlock, state: default);
            FastPathJsonSerializer.Deserialize<Receipt>(ref reader);
        });
        JsonException witnessError = Assert.ThrowsAny<JsonException>(() =>
        {
            Utf8JsonReader reader = new(Encoding.UTF8.GetBytes(json), isFinalBlock, state: default);
            FastPathJsonSerializer.Deserialize<Receipt, Receipt>(ref reader);
        });
        Assert.Equal(selfError.Message, witnessError.Message);
        if (expectedMessage is not null)
        {
            Assert.Equal(expectedMessage, selfError.Message);
        }
    }

    [Theory]
    [InlineData("[1,2,]")]
    [InlineData("[1,/* comment */2,]")]
    public static void ReaderOverloadUsesCallerOptions(string json)
    {
        Utf8JsonReader reader = new(Encoding.UTF8.GetBytes(json), new()
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip,
        });

        int[]? value = FastPathJsonSerializer.Deserialize<int[], TestCasesWitness>(ref reader);
        Assert.Equal(new[] { 1, 2 }, value);
        Assert.Equal(JsonTokenType.EndArray, reader.TokenType);
        Assert.False(reader.Read());
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
