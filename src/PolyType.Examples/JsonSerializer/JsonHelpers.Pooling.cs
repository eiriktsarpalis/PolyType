using PolyType.Examples.Utilities;
using System.Diagnostics;
using System.Text.Json;

namespace PolyType.Examples.JsonSerializer;

internal static partial class JsonHelpers
{
    [ThreadStatic]
    private static ThreadStaticWriteState? t_writeState;

    public static Utf8JsonWriter GetPooledJsonWriter(JsonWriterOptions options, out ByteBufferWriter bufferWriter)
    {
        ThreadStaticWriteState writeState = t_writeState ??= new();
        if (writeState.Depth++ == 0)
        {
            bufferWriter = writeState.BufferWriter;
            writeState.JsonWriter.Reset(bufferWriter, options);
            return writeState.JsonWriter;
        }

        bufferWriter = new(initialCapacity: 512);
        return new(bufferWriter, options);
    }

    public static void ReturnPooledJsonWriter(Utf8JsonWriter writer, ByteBufferWriter bufferWriter)
    {
        ThreadStaticWriteState? writeState = t_writeState;
        DebugExt.Assert(writeState is not null && writeState.Depth > 0);
        DebugExt.Assert(writeState.Depth != 1 || writeState.JsonWriter == writer && writeState.BufferWriter == bufferWriter);
        bufferWriter.ResetWrittenCount();
        writer.Reset();
        if (--writeState.Depth > 0)
        {
            writer.Dispose();
        }
    }

    private sealed class ThreadStaticWriteState
    {
        public int Depth;
        public ByteBufferWriter BufferWriter { get; } = new(initialCapacity: 512);
        public Utf8JsonWriter JsonWriter { get; } = new(Stream.Null);
    }
}
