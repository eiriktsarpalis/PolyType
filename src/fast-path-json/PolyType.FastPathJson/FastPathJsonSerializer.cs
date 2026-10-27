using PolyType.Examples.JsonSerializer;
using PolyType.Examples.Utilities;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace PolyType.FastPathJson;

/// <summary>Serializes JSON using statically selected converter witnesses.</summary>
public static class FastPathJsonSerializer
{
    /// <summary>Serializes a value using its generated implementation.</summary>
    /// <typeparam name="T">The self-serializing type.</typeparam>
    /// <param name="value">The value to serialize.</param>
    /// <param name="options">Options for the JSON writer.</param>
    /// <returns>The JSON document.</returns>
    public static string Serialize<T>(T? value, JsonWriterOptions options = default)
        where T : IJsonSerializable<T> =>
        Serialize<T, T>(value, options);

    /// <summary>Serializes a value using an explicit converter witness.</summary>
    /// <typeparam name="T">The serialized type.</typeparam>
    /// <typeparam name="TConverter">The converter witness.</typeparam>
    /// <param name="value">The value to serialize.</param>
    /// <param name="options">Options for the JSON writer.</param>
    /// <returns>The JSON document.</returns>
    public static string Serialize<T, TConverter>(T? value, JsonWriterOptions options = default)
        where TConverter : IJsonSerializable<T>
    {
        Utf8JsonWriter writer = JsonHelpers.GetPooledJsonWriter(options, out ByteBufferWriter bufferWriter);
        try
        {
            Serialize<T, TConverter>(writer, value);
            return Encoding.UTF8.GetString(bufferWriter.WrittenSpan);
        }
        finally
        {
            JsonHelpers.ReturnPooledJsonWriter(writer, bufferWriter);
        }
    }

    /// <summary>Serializes a value to the supplied writer using its generated implementation.</summary>
    /// <typeparam name="T">The self-serializing type.</typeparam>
    /// <param name="writer">The writer to receive the JSON value.</param>
    /// <param name="value">The value to serialize.</param>
    /// <remarks>The writer is flushed but is not disposed.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Serialize<T>(Utf8JsonWriter writer, T? value)
        where T : IJsonSerializable<T> =>
        Serialize<T, T>(writer, value);

    /// <summary>Serializes a value to the supplied writer using an explicit converter witness.</summary>
    /// <typeparam name="T">The serialized type.</typeparam>
    /// <typeparam name="TConverter">The converter witness.</typeparam>
    /// <param name="writer">The writer to receive the JSON value.</param>
    /// <param name="value">The value to serialize.</param>
    /// <remarks>The writer is flushed but is not disposed.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Serialize<T, TConverter>(Utf8JsonWriter writer, T? value)
        where TConverter : IJsonSerializable<T>
    {
        TConverter.Write(writer, value);
        writer.Flush();
    }

    /// <summary>Deserializes UTF-16 JSON using the type's generated implementation.</summary>
    /// <typeparam name="T">The self-serializing type.</typeparam>
    /// <param name="json">The JSON payload.</param>
    /// <param name="options">Options for the JSON reader.</param>
    /// <returns>The deserialized value.</returns>
    public static T? Deserialize<T>(ReadOnlySpan<char> json, JsonReaderOptions options = default)
        where T : IJsonSerializable<T> =>
        Deserialize<T, T>(json, options);

    /// <summary>Deserializes UTF-8 JSON using the type's generated implementation.</summary>
    /// <typeparam name="T">The self-serializing type.</typeparam>
    /// <param name="json">The JSON payload.</param>
    /// <param name="options">Options for the JSON reader.</param>
    /// <returns>The deserialized value.</returns>
    public static T? Deserialize<T>(ReadOnlySpan<byte> json, JsonReaderOptions options = default)
        where T : IJsonSerializable<T> =>
        Deserialize<T, T>(json, options);

    /// <summary>Deserializes UTF-16 JSON using an explicit converter witness.</summary>
    /// <typeparam name="T">The serialized type.</typeparam>
    /// <typeparam name="TConverter">The converter witness.</typeparam>
    /// <param name="json">The JSON payload.</param>
    /// <param name="options">Options for the JSON reader.</param>
    /// <returns>The deserialized value.</returns>
    public static T? Deserialize<T, TConverter>(ReadOnlySpan<char> json, JsonReaderOptions options = default)
        where TConverter : IJsonSerializable<T>
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(Encoding.UTF8.GetMaxByteCount(json.Length));
        try
        {
            int length = Encoding.UTF8.GetBytes(json, buffer);
            return Deserialize<T, TConverter>(buffer.AsSpan(0, length), options);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }

    /// <summary>Deserializes UTF-8 JSON using an explicit converter witness.</summary>
    /// <typeparam name="T">The serialized type.</typeparam>
    /// <typeparam name="TConverter">The converter witness.</typeparam>
    /// <param name="json">The JSON payload.</param>
    /// <param name="options">Options for the JSON reader.</param>
    /// <returns>The deserialized value.</returns>
    public static T? Deserialize<T, TConverter>(ReadOnlySpan<byte> json, JsonReaderOptions options = default)
        where TConverter : IJsonSerializable<T>
    {
        Utf8JsonReader reader = new(json, options);

        return Deserialize<T, TConverter>(ref reader);
    }

    /// <summary>Deserializes a value from the supplied reader using its generated implementation.</summary>
    /// <typeparam name="T">The self-serializing type.</typeparam>
    /// <param name="reader">The reader containing the JSON value.</param>
    /// <returns>The deserialized value.</returns>
    /// <remarks>
    /// Reads the current value, advancing first if the reader is unstarted or positioned on a property name.
    /// On return, the reader is positioned on the value's final token.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T? Deserialize<T>(ref Utf8JsonReader reader)
        where T : IJsonSerializable<T> =>
        Deserialize<T, T>(ref reader);

    /// <summary>Deserializes a value from the supplied reader using an explicit converter witness.</summary>
    /// <typeparam name="T">The serialized type.</typeparam>
    /// <typeparam name="TConverter">The converter witness.</typeparam>
    /// <param name="reader">The reader containing the JSON value.</param>
    /// <returns>The deserialized value.</returns>
    /// <remarks>
    /// Reads the current value, advancing first if the reader is unstarted or positioned on a property name.
    /// On return, the reader is positioned on the value's final token.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T? Deserialize<T, TConverter>(ref Utf8JsonReader reader)
        where TConverter : IJsonSerializable<T>
    {
        if (reader.TokenType is JsonTokenType.None or JsonTokenType.PropertyName && !reader.Read())
        {
            SerializationHelpers.ThrowJsonException("Expected a JSON value.");
        }

        return TConverter.Read(ref reader);
    }
}
