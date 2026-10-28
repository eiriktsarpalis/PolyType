using PolyType.FastPathJson.Converters;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace PolyType.FastPathJson;

/// <summary>Provides implementation helpers used by generated JSON serializers.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class SerializationHelpers
{
    /// <summary>Writes a single JSON value using an explicit converter witness.</summary>
    /// <typeparam name="T">The serialized type.</typeparam>
    /// <typeparam name="TConverter">The converter witness.</typeparam>
    /// <param name="writer">The writer to receive the value.</param>
    /// <param name="value">The value to serialize.</param>
    /// <remarks>The writer is not flushed or disposed.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write<T, TConverter>(Utf8JsonWriter writer, T? value)
        where TConverter : IJsonSerializable<T> =>
        TConverter.Write(writer, value);

    /// <summary>Reads a single JSON value, starting on its first token and returning on its final token.</summary>
    /// <typeparam name="T">The serialized type.</typeparam>
    /// <typeparam name="TConverter">The converter witness.</typeparam>
    /// <param name="reader">The reader positioned on the value's first token.</param>
    /// <returns>The deserialized value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T? Read<T, TConverter>(ref Utf8JsonReader reader)
        where TConverter : IJsonSerializable<T> =>
        TConverter.Read(ref reader);

    /// <summary>Writes a dictionary key using an explicit converter witness.</summary>
    /// <typeparam name="T">The key type.</typeparam>
    /// <typeparam name="TConverter">The key converter witness.</typeparam>
    /// <param name="writer">The writer to receive the property name.</param>
    /// <param name="key">The key to serialize.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteKey<T, TConverter>(Utf8JsonWriter writer, T key)
        where TConverter : IJsonSerializable<T> =>
        TConverter.WriteAsPropertyName(writer, key);

    /// <summary>Reads a dictionary key without advancing past the property-name token.</summary>
    /// <typeparam name="T">The key type.</typeparam>
    /// <typeparam name="TConverter">The key converter witness.</typeparam>
    /// <param name="reader">The reader positioned on a property name.</param>
    /// <returns>The deserialized key.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T ReadKey<T, TConverter>(ref Utf8JsonReader reader)
        where TConverter : IJsonSerializable<T> =>
        TConverter.ReadAsPropertyName(ref reader);

    /// <summary>Checks whether property names encoded with the default encoder can be reused.</summary>
    /// <param name="writer">The writer whose encoder is checked.</param>
    /// <returns>Whether the writer uses the default encoder.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool UsesDefaultEncoder(Utf8JsonWriter writer)
    {
        JavaScriptEncoder? encoder = writer.Options.Encoder;

        return encoder is null || ReferenceEquals(encoder, JavaScriptEncoder.Default);
    }

    /// <summary>Gets the unescaped UTF-8 property name, borrowing contiguous unescaped input when possible.</summary>
    /// <param name="reader">The reader positioned on a property name.</param>
    /// <param name="buffer">Scratch space for escaped or segmented names.</param>
    /// <param name="rented">A reusable pooled buffer, returned by the caller to <see cref="System.Buffers.ArrayPool{T}.Shared"/>.</param>
    /// <returns>The property name, valid until the scratch space or input is reused.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<byte> GetUtf8PropertyName(scoped in Utf8JsonReader reader, Span<byte> buffer, scoped ref byte[]? rented)
    {
        if (!reader.HasValueSequence && !reader.ValueIsEscaped)
        {
            ReadOnlySpan<byte> name = reader.ValueSpan;
            if (!System.Text.Unicode.Utf8.IsValid(name))
            {
                ThrowInvalidUtf8PropertyName();
            }

            return name;
        }

        return ScalarHelpers.GetUtf8Value(in reader, buffer, ref rented);

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        static void ThrowInvalidUtf8PropertyName() =>
            throw new InvalidOperationException("Cannot transcode invalid UTF-8 JSON text to UTF-16 string.");
    }

    /// <summary>Gets a UTF-8 property name without retaining a pooled buffer.</summary>
    /// <param name="reader">The reader positioned on a property name.</param>
    /// <param name="buffer">Scratch space large enough for every matching name, including its longest escaped representation.</param>
    /// <param name="name">The unescaped property name when successful.</param>
    /// <returns>False for escaped or segmented names whose raw length exceeds the scratch space; otherwise true.</returns>
    /// <remarks>Oversized names are still validated, but cannot match the caller's known names.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetUtf8PropertyName(scoped in Utf8JsonReader reader, Span<byte> buffer, out ReadOnlySpan<byte> name)
    {
        if ((!reader.HasValueSequence && !reader.ValueIsEscaped) || ScalarHelpers.GetValueLength(in reader) <= buffer.Length)
        {
            byte[]? rented = null;
            name = GetUtf8PropertyName(in reader, buffer, ref rented);

            return true;
        }

        ValidateOversizedPropertyName(in reader);
        name = default;

        return false;

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void ValidateOversizedPropertyName(scoped in Utf8JsonReader reader)
        {
            byte[]? rented = null;
            try
            {
                _ = ScalarHelpers.GetUtf8Value(in reader, Span<byte>.Empty, ref rented);
            }
            finally
            {
                if (rented is not null)
                {
                    System.Buffers.ArrayPool<byte>.Shared.Return(rented);
                }
            }
        }
    }

    /// <summary>Advances the reader, throwing if the JSON value is incomplete.</summary>
    /// <param name="reader">The reader to advance.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void EnsureRead(ref Utf8JsonReader reader)
    {
        if (!reader.Read())
        {
            ThrowIncompleteJsonValue();
        }

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        static void ThrowIncompleteJsonValue() => throw new JsonException("Incomplete JSON value.");
    }

    /// <summary>Checks that the reader is positioned on the expected token.</summary>
    /// <param name="reader">The reader whose current token is checked.</param>
    /// <param name="expected">The required token type.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void EnsureToken(ref Utf8JsonReader reader, JsonTokenType expected)
    {
        if (reader.TokenType != expected)
        {
            ThrowUnexpectedToken(expected, reader.TokenType);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void ThrowUnexpectedToken(JsonTokenType expected, JsonTokenType actual) =>
            throw new JsonException($"Expected {expected}, got {actual}.");
    }

    /// <summary>Checks whether all bits in a leading segment are set.</summary>
    /// <param name="seen">The tracked member-presence bits.</param>
    /// <param name="count">The number of leading bits to examine, between zero and the array's length.</param>
    /// <returns>Whether every examined bit is set.</returns>
    public static bool HasAllSet(BitArray seen, int count)
    {
        ReadOnlySpan<byte> bytes = CollectionsMarshal.AsBytes(seen);
        int fullBytes = count / 8;
        if (bytes.Slice(0, fullBytes).IndexOfAnyExcept(byte.MaxValue) >= 0)
        {
            return false;
        }

        int remainder = count % 8;
        if (remainder == 0)
        {
            return true;
        }

        uint mask = (1u << remainder) - 1;

        return (bytes[fullBytes] & mask) == mask;
    }

    /// <summary>Reports a null value obtained from a non-nullable member during serialization.</summary>
    /// <param name="name">The JSON member name.</param>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowNullPropertyOnWrite(string name) =>
        throw new JsonException($"The property '{name}' cannot contain null.");

    /// <summary>Reports a null value supplied for a non-nullable member during deserialization.</summary>
    /// <param name="name">The JSON member name.</param>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowNullPropertyOnRead(string name) =>
        throw new JsonException($"The property '{name}' cannot be set to null.");

    /// <summary>Reports all missing required constructor parameters or properties.</summary>
    /// <param name="names">The nonempty list of missing JSON member names.</param>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowMissingRequiredParameters(List<string> names)
    {
        if (names.Count == 1)
        {
            throw new KeyNotFoundException($"Missing required constructor parameter '{names[0]}'.");
        }

        throw new KeyNotFoundException($"Missing required constructor parameters: '{string.Join("', '", names)}'.");
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowJsonException(string message) => throw new JsonException(message);

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static T ThrowJsonException<T>(string message) => throw new JsonException(message);

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static T ThrowUnsupportedPropertyName<T>() =>
        throw new NotSupportedException($"'{typeof(T)}' is not supported as a JSON property name.");

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static T ThrowInvalidOperationException<T>(string message) => throw new InvalidOperationException(message);
}
