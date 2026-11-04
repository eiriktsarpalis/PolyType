using PolyType.Examples.JsonSerializer;
using PolyType.FastPathJson.Converters;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace PolyType.FastPathJson.Tests;

public static class WriterPoolingTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public static void ReusesWriterAndResetsOptions(bool indented, bool useFastPathJson)
    {
        string[] value = ["<", ">"];
        Utf8JsonWriter? previous = null;
        foreach (bool useIndentation in new[] { indented, !indented, indented })
        {
            var encoder = useIndentation ? JavaScriptEncoder.UnsafeRelaxedJsonEscaping : JavaScriptEncoder.Default;
            Utf8JsonWriter? current = null;
            string json = Serialize(writer =>
            {
                current = writer;
                ArrayConverter<string, StringConverter>.Write(writer, value);
            }, new() { Indented = useIndentation, Encoder = encoder }, useFastPathJson);

            Assert.NotNull(current);
            if (previous is not null)
            {
                Assert.Same(previous, current);
            }

            Assert.Equal(JsonSerializer.Serialize(value, new JsonSerializerOptions
            {
                WriteIndented = useIndentation,
                Encoder = encoder,
            }), json);
            previous = current;
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public static void FailedSerializationReturnsWriter(bool flush, bool useFastPathJson)
    {
        Utf8JsonWriter? failed = null;
        Assert.Throws<InvalidOperationException>(() => Serialize(writer =>
        {
            failed = writer;
            writer.WriteStartArray();
            writer.WriteStringValue(new string('x', 4096));
            if (flush)
            {
                writer.Flush();
            }

            throw new InvalidOperationException("Converter failure.");
        }, useFastPathJson: useFastPathJson));

        Utf8JsonWriter? recovered = null;
        string json = Serialize(writer =>
        {
            recovered = writer;
            writer.WriteNumberValue(42);
        }, useFastPathJson: useFastPathJson);
        Assert.NotNull(failed);
        Assert.Same(failed, recovered);
        Assert.Equal("42", json);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public static void NestedSerializationDoesNotOverwriteOuterState(bool failInner, bool useFastPathJson)
    {
        Utf8JsonWriter? outer = null;
        Utf8JsonWriter? inner = null;
        string json = Serialize(writer =>
        {
            outer = writer;
            writer.WriteStartObject();
            if (failInner)
            {
                Assert.Throws<InvalidOperationException>(() => Serialize(nested =>
                {
                    inner = nested;
                    nested.WriteStartArray();
                    nested.WriteNumberValue(42);
                    nested.Flush();
                    throw new InvalidOperationException("Nested converter failure.");
                }, useFastPathJson: useFastPathJson));
                writer.WriteBoolean("failed", true);
            }
            else
            {
                string nestedJson = Serialize(nested =>
                {
                    inner = nested;
                    nested.WriteNumberValue(42);
                }, new() { Indented = true }, useFastPathJson);
                writer.WriteString("nested", nestedJson);
            }

            writer.WriteNumber("outer", 7);
            writer.WriteEndObject();
        }, useFastPathJson: useFastPathJson);

        Assert.NotNull(outer);
        Assert.NotNull(inner);
        Assert.NotSame(outer, inner);
        Assert.Equal(failInner ? """{"failed":true,"outer":7}""" : """{"nested":"42","outer":7}""", json);

        Utf8JsonWriter? reused = null;
        Assert.Equal("true", Serialize(writer =>
        {
            reused = writer;
            writer.WriteBooleanValue(true);
        }, useFastPathJson: useFastPathJson));
        Assert.Same(outer, reused);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(512, false)]
    [InlineData(512, true)]
    [InlineData(4096, false)]
    [InlineData(4096, true)]
    public static void BufferCanGrowAndBeReused(int length, bool useFastPathJson)
    {
        foreach (string value in new[] { new string('x', length), "reused", "" })
        {
            Assert.Equal(JsonSerializer.Serialize(value),
                Serialize(writer => StringConverter.Write(writer, value), useFastPathJson: useFastPathJson));
        }
    }

    private static readonly WriterProbeConverter s_probeConverter = new();

    private static string Serialize(Action<Utf8JsonWriter> write, JsonWriterOptions options = default, bool useFastPathJson = true) =>
        useFastPathJson
            ? FastPathJsonSerializer.Serialize<Action<Utf8JsonWriter>, WriterProbe>(write, options)
            : JsonSerializerTS.Serialize(s_probeConverter, write, options);

    private sealed class WriterProbe : IJsonSerializable<Action<Utf8JsonWriter>>
    {
        public static void Write(Utf8JsonWriter writer, Action<Utf8JsonWriter>? value) => value!(writer);
        public static Action<Utf8JsonWriter>? Read(ref Utf8JsonReader reader) => throw new NotSupportedException();
    }

    private sealed class WriterProbeConverter : JsonConverter<Action<Utf8JsonWriter>>
    {
        public override void Write(Utf8JsonWriter writer, Action<Utf8JsonWriter> value, JsonSerializerOptions options) => value(writer);
        public override Action<Utf8JsonWriter>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            throw new NotSupportedException();
    }
}
