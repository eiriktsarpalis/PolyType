using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace PolyType.FastPathJson.Converters;

internal static class ScalarHelpers
{
    public const int StackBufferSize = 256;

    public static int GetValueLength(scoped in Utf8JsonReader reader) =>
        checked((int)(reader.HasValueSequence ? reader.ValueSequence.Length : reader.ValueSpan.Length));

    public static ReadOnlySpan<byte> GetUtf8Value(scoped in Utf8JsonReader reader, Span<byte> buffer, ref byte[]? rented)
    {
        if (!reader.HasValueSequence && !reader.ValueIsEscaped)
        {
            return reader.ValueSpan;
        }

        int length = GetValueLength(in reader);
        Span<byte> destination = length <= buffer.Length ? buffer : (rented = ArrayPool<byte>.Shared.Rent(length));
        if (reader.TokenType is JsonTokenType.Number)
        {
            reader.ValueSequence.CopyTo(destination);
            return destination[..length];
        }

        return destination[..reader.CopyString(destination)];
    }

    public static T ReadNumberPropertyName<T>(ref Utf8JsonReader reader, NumberStyles styles)
        where T : struct, INumberBase<T>
    {
        Span<byte> buffer = stackalloc byte[StackBufferSize];
        byte[]? rented = null;
        try
        {
            ReadOnlySpan<byte> source = GetUtf8Value(in reader, buffer, ref rented);
            if (!T.TryParse(source, styles, CultureInfo.InvariantCulture, out T result))
            {
                ThrowFormatException();
            }

            if (T.IsNaN(result) && !source.SequenceEqual("NaN"u8) ||
                T.IsPositiveInfinity(result) && !source.SequenceEqual("Infinity"u8) ||
                T.IsNegativeInfinity(result) && !source.SequenceEqual("-Infinity"u8))
            {
                ThrowFormatException();
            }

            return result;
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    public static void WriteNumberPropertyName<T>(Utf8JsonWriter writer, T value)
        where T : struct, IUtf8SpanFormattable
    {
        Span<byte> buffer = stackalloc byte[64];
        if (!value.TryFormat(buffer, out int written, format: default, provider: CultureInfo.InvariantCulture))
        {
            ThrowFormattingException();
        }

        if (typeof(T) == typeof(Half) || typeof(T) == typeof(Int128) || typeof(T) == typeof(UInt128))
        {
            writer.WritePropertyName(buffer[..written]);
        }
        else
        {
            WriteUnescapedPropertyName(writer, buffer[..written]);
        }
    }

    public static void WriteUnescapedPropertyName(Utf8JsonWriter writer, ReadOnlySpan<byte> value)
    {
        // STJ's typed key overloads bypass encoders for these formatter-owned ASCII names.
        if (SerializationHelpers.UsesDefaultEncoder(writer) && value.IndexOf((byte)'+') < 0)
        {
            writer.WritePropertyName(value);
        }
        else
        {
            writer.WritePropertyName(JsonEncodedText.Encode(value, JavaScriptEncoder.UnsafeRelaxedJsonEscaping));
        }
    }

    public static int CopyQuotedPropertyName(scoped ref Utf8JsonReader reader, scoped Span<byte> buffer)
    {
        int length = GetValueLength(in reader);
        if (length > buffer.Length - 2)
        {
            ThrowFormatException();
        }

        buffer[0] = (byte)'"';
        if (reader.HasValueSequence)
        {
            reader.ValueSequence.CopyTo(buffer[1..]);
        }
        else
        {
            reader.ValueSpan.CopyTo(buffer[1..]);
        }

        buffer[length + 1] = (byte)'"';

        return length + 2;
    }

    public static int TrimDateTimeFraction(Span<byte> buffer, int written)
    {
        int end = 27;
        while (end > 20 && buffer[end - 1] == (byte)'0')
        {
            end--;
        }

        if (end == 20)
        {
            end = 19;
        }

        buffer[27..written].CopyTo(buffer[end..]);

        return end + written - 27;
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowFormatException() => throw new FormatException("The JSON scalar value has an invalid format.");

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowInvalidStringToken(JsonTokenType token) =>
        throw new InvalidOperationException($"Expected a JSON string, got {token}.");

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowInvalidNumberToken(JsonTokenType token) =>
        throw new InvalidOperationException($"Expected a JSON number, got {token}.");

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowFormattingException() => throw new InvalidOperationException("Could not format the JSON scalar value.");

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowNonFiniteNumber() => throw new ArgumentException("Non-finite numbers cannot be written as JSON.");
}
