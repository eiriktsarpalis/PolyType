using Microsoft.CodeAnalysis;
using PolyType.Tests;
using System.Buffers;
using System.Runtime.Loader;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Xunit;

namespace PolyType.FastPathJson.Tests;

public static class PropertyNameReadingTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public static void PropertyNamesPreserveEscapingAndExactMatching(bool escaped, bool segmented)
    {
        Dictionary<string, object> properties = new()
        {
            ["inherited<name>"] = 1,
            ["ascii"] = "plain",
            ["<name>"] = "html",
            ["quote\"\\\n\0"] = "escaped",
            ["\u00E9\uD83E\uDD2F"] = "unicode",
            [""] = "empty",
            ["Ascii"] = "ignored",
            ["ascij"] = new[] { 1, 2 },
            ["<namf>"] = new { Nested = true },
            ["\u00E9\uD83E\uDD2E"] = "ignored",
            [new string('a', 300)] = new { Nested = new[] { 1, 2 } },
            [new string('\u00E9', 600)] = "ignored",
            [new string('b', 400)] = false,
        };
        JsonSerializerOptions options = new()
        {
            Encoder = escaped ? JavaScriptEncoder.Default : JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(properties, options);
        CachedNamePayload? value = Read<CachedNamePayload, CachedNamesWitness>(CreateSequence(json, segmented));

        Assert.NotNull(value);
        Assert.Equal(1, value.Inherited);
        Assert.Equal("plain", value.Plain);
        Assert.Equal("html", value.Html);
        Assert.Equal("escaped", value.Escaped);
        Assert.Equal("unicode", value.Unicode);
        Assert.Equal("empty", value.Empty);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public static void SameLengthNamesAndDuplicatesMatchExactly(bool escaped, bool segmented)
    {
        string json = """{"Item0":0,"Item9":9,"Item8":8,"Item7":7,"Item6":6,"Item5":5,"Item4":4,"Item3":3,"Item2":2,"Item1":1,"Item1":42,"ItemX":99,"Item10":10}""";
        if (escaped)
        {
            json = json.Replace("Item", @"\u0049\u0074\u0065\u006D", StringComparison.Ordinal);
        }

        var value = Read<(int, int, int, int, int, int, int, int, int), TestCasesWitness>(
            CreateSequence(Encoding.UTF8.GetBytes(json), segmented));
        Assert.Equal((42, 2, 3, 4, 5, 6, 7, 8, 9), value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void PropertyNamesDoNotAllocateStrings(bool segmented)
    {
        ReadOnlySequence<byte> baseline = CreateSequence("""{"Value":0,"Value":42}"""u8.ToArray(), segmented);
        string longName = new('a', 600);
        string json = $$$"""{"{{{longName}}}":[],"Value":0,"\u0056alue":42,"{{{longName}}}":{}}""";
        ReadOnlySequence<byte> repeatedNames = CreateSequence(Encoding.UTF8.GetBytes(json), segmented);
        Measure(baseline);
        Measure(repeatedNames);

        var expected = Measure(baseline);
        var actual = Measure(repeatedNames);
        Assert.Equal(42_000, expected.Sum);
        Assert.Equal(expected.Sum, actual.Sum);
        // Allow pool/profiler bookkeeping, but less than one additional string allocation per read.
        Assert.InRange(actual.AllocatedBytes - expected.AllocatedBytes, -16_000L, 16_000L);

        static (long AllocatedBytes, int Sum) Measure(ReadOnlySequence<byte> json)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            int sum = 0;
            for (int i = 0; i < 1000; i++)
            {
                Utf8JsonReader reader = new(json);
                sum += FastPathJsonSerializer.Deserialize<SimplePoco, TestCasesWitness>(ref reader)!.Value;
            }

            return (GC.GetAllocatedBytesForCurrentThread() - before, sum);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public static void InvalidPropertyNameTextIsRejected(bool escaped, bool segmented)
    {
        byte[] json = escaped
            ? """{"\uD800":0}"""u8.ToArray()
            : [(byte)'{', (byte)'"', 0xFF, (byte)'"', (byte)':', (byte)'0', (byte)'}'];
        ReadOnlySequence<byte> sequence = CreateSequence(json, segmented);
        Assert.Throws<InvalidOperationException>(() => Read<SimplePoco, TestCasesWitness>(sequence));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public static void LongNamesAndUnpairedSurrogateMetadataPreserveBehavior(bool escaped, bool segmented)
    {
        string longName = new('a', 600);
        string source = $$"""
            [GenerateJson]
            public partial class Payload
            {
                [System.Text.Json.Serialization.JsonPropertyName("{{longName}}")]
                public int Value { get; set; }

                [System.Text.Json.Serialization.JsonPropertyName("\uD800")]
                public int Unreachable { get; set; } = -1;
            }

            public static class Consumer
            {
                public static int Read(ref System.Text.Json.Utf8JsonReader reader)
                {
                    Payload value = FastPathJsonSerializer.Deserialize<Payload>(ref reader)!;
                    return value.Value + value.Unreachable;
                }
            }
            """;
        var result = CompilationHelpers.Run(source);
        using MemoryStream assemblyStream = new();
        var emitted = result.Compilation.Emit(assemblyStream, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics));
        assemblyStream.Position = 0;
        AssemblyLoadContext context = new("PropertyNameReader", isCollectible: true);
        try
        {
            var assembly = context.LoadFromStream(assemblyStream);
            GeneratedReader deserialize = assembly.GetType("Consumer")!.GetMethod("Read")!.CreateDelegate<GeneratedReader>();
            string jsonName = escaped ? string.Concat(Enumerable.Repeat(@"\u0061", 600)) : longName;
            string json = $$"""{"{{jsonName}}":42,"\uFFFD":99}""";
            Utf8JsonReader reader = new(CreateSequence(Encoding.UTF8.GetBytes(json), segmented));
            Assert.Equal(41, deserialize(ref reader));
            Assert.Equal(JsonTokenType.EndObject, reader.TokenType);
            Assert.False(reader.Read());
        }
        finally
        {
            context.Unload();
        }
    }

    private delegate int GeneratedReader(ref Utf8JsonReader reader);

    private static T? Read<T, TWitness>(ReadOnlySequence<byte> json)
        where TWitness : IJsonSerializable<T>
    {
        Utf8JsonReader reader = new(json);
        T? value = FastPathJsonSerializer.Deserialize<T, TWitness>(ref reader);
        Assert.Equal(JsonTokenType.EndObject, reader.TokenType);
        Assert.False(reader.Read());

        return value;
    }

    private static ReadOnlySequence<byte> CreateSequence(byte[] bytes, bool segmented)
    {
        if (!segmented)
        {
            return new(bytes);
        }

        Segment first = new(bytes.AsMemory(0, 1));
        Segment last = first;
        for (int i = 1; i < bytes.Length; i++)
        {
            last = last.Append(bytes.AsMemory(i, 1));
        }

        return new(first, 0, last, last.Memory.Length);
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
