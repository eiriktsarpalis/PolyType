using PolyType.Examples.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PolyType.Examples.JsonSerializer;

/// <summary>
/// Represents a custom JSON converter that implements <see cref="IMarshaller{T, TSurrogate}"/>.
/// </summary>
/// <typeparam name="T">The type of the marshalled value.</typeparam>
public abstract class JsonMarshaller<T> : JsonConverter<T>, IMarshaller<T, JsonNode>
{
    // The implementation of the marshaller interface is here to provide a modicum of compatibility
    // with non-JSON consuming libraries. We might as well have them throw a NotSupportedException.

    T? IMarshaller<T, JsonNode>.FromSurrogate(JsonNode? surrogate)
    {
        byte[] utf8Json = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(surrogate, JsonNodeContext.Default.JsonNode!);
        Utf8JsonReader reader = new(utf8Json);
        return Read(ref reader, typeof(T), JsonSerializerTS.s_options);
    }

    JsonNode? IMarshaller<T, JsonNode>.ToSurrogate(T? value)
    {
        if (value is null)
        {
            return null;
        }

        Utf8JsonWriter writer = JsonHelpers.GetPooledJsonWriter(default, out ByteBufferWriter bufferWriter);
        try
        {
            Write(writer, value, JsonSerializerTS.s_options);
            return JsonNode.Parse(bufferWriter.WrittenSpan);
        }
        finally
        {
            JsonHelpers.ReturnPooledJsonWriter(writer, bufferWriter);
        }
    }
}


[JsonSerializable(typeof(JsonNode))]
internal sealed partial class JsonNodeContext : JsonSerializerContext;