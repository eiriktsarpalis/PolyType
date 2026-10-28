using PolyType.Tests;
using System.Buffers;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json;
using Xunit;

namespace PolyType.FastPathJson.Tests;

public static class PropertyNameBufferTests
{
    [Theory]
    [InlineData(42, false)]
    [InlineData(42, true)]
    [InlineData(43, false)]
    [InlineData(43, true)]
    public static void MaximallyEscapedNamesRoundtripAtTheStackBufferBoundary(int length, bool segmented)
    {
        string source = $$"""
            [GenerateJson]
            public partial class Payload
            {
                [System.Text.Json.Serialization.JsonPropertyName("{{new string('\u00E9', length)}}")]
                public int Value { get; set; }
            }

            public static class Consumer
            {
                public static int Read(ref System.Text.Json.Utf8JsonReader reader) =>
                    FastPathJsonSerializer.Deserialize<Payload>(ref reader)!.Value;
            }
            """;
        var result = CompilationHelpers.Run(source);
        using MemoryStream assemblyStream = new();
        var emitted = result.Compilation.Emit(assemblyStream, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics));
        assemblyStream.Position = 0;
        AssemblyLoadContext context = new("PropertyNameBufferBoundary", isCollectible: true);
        try
        {
            var assembly = context.LoadFromStream(assemblyStream);
            GeneratedReader deserialize = assembly.GetType("Consumer")!.GetMethod("Read")!.CreateDelegate<GeneratedReader>();
            string name = string.Concat(Enumerable.Repeat(@"\u00E9", length));
            string oversizedUnknownName = string.Concat(Enumerable.Repeat(@"\u0061", 300));
            string json = $$$"""{"{{{oversizedUnknownName}}}":{},"{{{name}}}":42,"":99,"{{{oversizedUnknownName}}}":[]}""";
            Utf8JsonReader reader = new(CreateSequence(Encoding.UTF8.GetBytes(json), segmented));
            Assert.Equal(42, deserialize(ref reader));
            Assert.Equal(JsonTokenType.EndObject, reader.TokenType);
            Assert.False(reader.Read());
        }
        finally
        {
            context.Unload();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void OversizedUnknownNamesStillRejectInvalidUnicode(bool segmented)
    {
        string name = new string('a', 300) + @"\uD800";
        string json = $$"""{"{{name}}":0,"Value":42}""";
        ReadOnlySequence<byte> sequence = CreateSequence(Encoding.UTF8.GetBytes(json), segmented);
        Assert.Throws<InvalidOperationException>(() =>
        {
            Utf8JsonReader reader = new(sequence);
            FastPathJsonSerializer.Deserialize<SimplePoco, TestCasesWitness>(ref reader);
        });
    }

    private delegate int GeneratedReader(ref Utf8JsonReader reader);

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
