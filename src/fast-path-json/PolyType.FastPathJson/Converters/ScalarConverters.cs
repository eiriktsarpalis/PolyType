using System.Buffers;
using System.Buffers.Text;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;

namespace PolyType.FastPathJson.Converters;

/// <summary>Serializes Boolean values and property names.</summary>
public sealed class BooleanConverter : IJsonSerializable<bool>
{
    private BooleanConverter() { }
    /// <inheritdoc/>
    public static void Write(Utf8JsonWriter writer, bool value) => writer.WriteBooleanValue(value);
    /// <inheritdoc/>
    public static bool Read(ref Utf8JsonReader reader) => reader.GetBoolean();
    /// <inheritdoc/>
    public static void WriteAsPropertyName(Utf8JsonWriter writer, bool value) =>
        ScalarHelpers.WriteUnescapedPropertyName(writer, value ? "True"u8 : "False"u8);
    /// <inheritdoc/>
    public static bool ReadAsPropertyName(ref Utf8JsonReader reader)
    {
        const int MaxEscapedBooleanLength = 5 * 6;
        if (ScalarHelpers.GetValueLength(in reader) > MaxEscapedBooleanLength)
        {
            ScalarHelpers.ThrowFormatException();
        }

        Span<byte> buffer = stackalloc byte[MaxEscapedBooleanLength];
        int written = reader.CopyString(buffer);
        if (!Utf8Parser.TryParse(buffer[..written], out bool value, out int consumed) || consumed != written)
        {
            ScalarHelpers.ThrowFormatException();
        }

        return value;
    }
}

/// <summary>Serializes signed 8-bit integers and property names.</summary>
public sealed class SByteConverter : IJsonSerializable<sbyte>
{
    private SByteConverter() { }
    /// <inheritdoc/>
    public static void Write(Utf8JsonWriter writer, sbyte value) => writer.WriteNumberValue(value);
    /// <inheritdoc/>
    public static sbyte Read(ref Utf8JsonReader reader) => reader.GetSByte();
    /// <inheritdoc/>
    public static void WriteAsPropertyName(Utf8JsonWriter writer, sbyte value) => ScalarHelpers.WriteNumberPropertyName(writer, value);
    /// <inheritdoc/>
    public static sbyte ReadAsPropertyName(ref Utf8JsonReader reader) => ScalarHelpers.ReadNumberPropertyName<sbyte>(ref reader, NumberStyles.AllowLeadingSign);
}

/// <summary>Serializes signed 16-bit integers and property names.</summary>
public sealed class Int16Converter : IJsonSerializable<short>
{
    private Int16Converter() { }
    /// <inheritdoc/>
    public static void Write(Utf8JsonWriter writer, short value) => writer.WriteNumberValue(value);
    /// <inheritdoc/>
    public static short Read(ref Utf8JsonReader reader) => reader.GetInt16();
    /// <inheritdoc/>
    public static void WriteAsPropertyName(Utf8JsonWriter writer, short value) => ScalarHelpers.WriteNumberPropertyName(writer, value);
    /// <inheritdoc/>
    public static short ReadAsPropertyName(ref Utf8JsonReader reader) => ScalarHelpers.ReadNumberPropertyName<short>(ref reader, NumberStyles.AllowLeadingSign);
}

/// <summary>Serializes signed 32-bit integers and property names.</summary>
public readonly struct Int32Converter : IJsonSerializable<int>
{
    /// <inheritdoc/>
    public static void Write(Utf8JsonWriter writer, int value) => writer.WriteNumberValue(value);
    /// <inheritdoc/>
    public static int Read(ref Utf8JsonReader reader) => reader.GetInt32();
    /// <inheritdoc/>
    public static void WriteAsPropertyName(Utf8JsonWriter writer, int value) => ScalarHelpers.WriteNumberPropertyName(writer, value);
    /// <inheritdoc/>
    public static int ReadAsPropertyName(ref Utf8JsonReader reader) => ScalarHelpers.ReadNumberPropertyName<int>(ref reader, NumberStyles.AllowLeadingSign);
}

/// <summary>Serializes signed 64-bit integers and property names.</summary>
public sealed class Int64Converter : IJsonSerializable<long>
{
    private Int64Converter() { }
    /// <inheritdoc/>
    public static void Write(Utf8JsonWriter writer, long value) => writer.WriteNumberValue(value);
    /// <inheritdoc/>
    public static long Read(ref Utf8JsonReader reader) => reader.GetInt64();
    /// <inheritdoc/>
    public static void WriteAsPropertyName(Utf8JsonWriter writer, long value) => ScalarHelpers.WriteNumberPropertyName(writer, value);
    /// <inheritdoc/>
    public static long ReadAsPropertyName(ref Utf8JsonReader reader) => ScalarHelpers.ReadNumberPropertyName<long>(ref reader, NumberStyles.AllowLeadingSign);
}

/// <summary>Serializes unsigned 8-bit integers and property names.</summary>
public sealed class ByteConverter : IJsonSerializable<byte>
{
    private ByteConverter() { }
    /// <inheritdoc/>
    public static void Write(Utf8JsonWriter writer, byte value) => writer.WriteNumberValue(value);
    /// <inheritdoc/>
    public static byte Read(ref Utf8JsonReader reader) => reader.GetByte();
    /// <inheritdoc/>
    public static void WriteAsPropertyName(Utf8JsonWriter writer, byte value) => ScalarHelpers.WriteNumberPropertyName(writer, value);
    /// <inheritdoc/>
    public static byte ReadAsPropertyName(ref Utf8JsonReader reader) => ScalarHelpers.ReadNumberPropertyName<byte>(ref reader, NumberStyles.None);
}

/// <summary>Serializes unsigned 16-bit integers and property names.</summary>
public sealed class UInt16Converter : IJsonSerializable<ushort>
{
    private UInt16Converter() { }
    /// <inheritdoc/>
    public static void Write(Utf8JsonWriter writer, ushort value) => writer.WriteNumberValue(value);
    /// <inheritdoc/>
    public static ushort Read(ref Utf8JsonReader reader) => reader.GetUInt16();
    /// <inheritdoc/>
    public static void WriteAsPropertyName(Utf8JsonWriter writer, ushort value) => ScalarHelpers.WriteNumberPropertyName(writer, value);
    /// <inheritdoc/>
    public static ushort ReadAsPropertyName(ref Utf8JsonReader reader) => ScalarHelpers.ReadNumberPropertyName<ushort>(ref reader, NumberStyles.None);
}

/// <summary>Serializes unsigned 32-bit integers and property names.</summary>
public sealed class UInt32Converter : IJsonSerializable<uint>
{
    private UInt32Converter() { }
    /// <inheritdoc/>
    public static void Write(Utf8JsonWriter writer, uint value) => writer.WriteNumberValue(value);
    /// <inheritdoc/>
    public static uint Read(ref Utf8JsonReader reader) => reader.GetUInt32();
    /// <inheritdoc/>
    public static void WriteAsPropertyName(Utf8JsonWriter writer, uint value) => ScalarHelpers.WriteNumberPropertyName(writer, value);
    /// <inheritdoc/>
    public static uint ReadAsPropertyName(ref Utf8JsonReader reader) => ScalarHelpers.ReadNumberPropertyName<uint>(ref reader, NumberStyles.None);
}

/// <summary>Serializes unsigned 64-bit integers and property names.</summary>
public sealed class UInt64Converter : IJsonSerializable<ulong>
{
    private UInt64Converter() { }
    /// <inheritdoc/>
    public static void Write(Utf8JsonWriter writer, ulong value) => writer.WriteNumberValue(value);
    /// <inheritdoc/>
    public static ulong Read(ref Utf8JsonReader reader) => reader.GetUInt64();
    /// <inheritdoc/>
    public static void WriteAsPropertyName(Utf8JsonWriter writer, ulong value) => ScalarHelpers.WriteNumberPropertyName(writer, value);
    /// <inheritdoc/>
    public static ulong ReadAsPropertyName(ref Utf8JsonReader reader) => ScalarHelpers.ReadNumberPropertyName<ulong>(ref reader, NumberStyles.None);
}

/// <summary>Serializes single-precision numbers and property names.</summary>
public sealed class SingleConverter : IJsonSerializable<float>
{
    private SingleConverter() { }
    /// <inheritdoc/>
    public static void Write(Utf8JsonWriter writer, float value) => writer.WriteNumberValue(value);
    /// <inheritdoc/>
    public static float Read(ref Utf8JsonReader reader) => reader.GetSingle();
    /// <inheritdoc/>
    public static void WriteAsPropertyName(Utf8JsonWriter writer, float value) => ScalarHelpers.WriteNumberPropertyName(writer, value);
    /// <inheritdoc/>
    public static float ReadAsPropertyName(ref Utf8JsonReader reader) => ScalarHelpers.ReadNumberPropertyName<float>(ref reader, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent);
}

/// <summary>Serializes double-precision numbers and property names.</summary>
public sealed class DoubleConverter : IJsonSerializable<double>
{
    private DoubleConverter() { }
    /// <inheritdoc/>
    public static void Write(Utf8JsonWriter writer, double value) => writer.WriteNumberValue(value);
    /// <inheritdoc/>
    public static double Read(ref Utf8JsonReader reader) => reader.GetDouble();
    /// <inheritdoc/>
    public static void WriteAsPropertyName(Utf8JsonWriter writer, double value)
    {
        if (!double.IsFinite(value))
        {
            ScalarHelpers.ThrowNonFiniteNumber();
        }

        ScalarHelpers.WriteNumberPropertyName(writer, value);
    }
    /// <inheritdoc/>
    public static double ReadAsPropertyName(ref Utf8JsonReader reader) => ScalarHelpers.ReadNumberPropertyName<double>(ref reader, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent);
}

/// <summary>Serializes decimal numbers and property names.</summary>
public sealed class DecimalConverter : IJsonSerializable<decimal>
{
    private DecimalConverter() { }
    /// <inheritdoc/>
    public static void Write(Utf8JsonWriter writer, decimal value) => writer.WriteNumberValue(value);
    /// <inheritdoc/>
    public static decimal Read(ref Utf8JsonReader reader) => reader.GetDecimal();
    /// <inheritdoc/>
    public static void WriteAsPropertyName(Utf8JsonWriter writer, decimal value) => ScalarHelpers.WriteNumberPropertyName(writer, value);
    /// <inheritdoc/>
    public static decimal ReadAsPropertyName(ref Utf8JsonReader reader) => ScalarHelpers.ReadNumberPropertyName<decimal>(ref reader, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent);
}

/// <summary>Serializes characters and property names.</summary>
public sealed class CharConverter : IJsonSerializable<char>
{
    private CharConverter() { }
    /// <inheritdoc/>
    public static void Write(Utf8JsonWriter writer, char value) => writer.WriteStringValue(new ReadOnlySpan<char>(in value));
    /// <inheritdoc/>
    public static char Read(ref Utf8JsonReader reader)
    {
        if (reader.TokenType is not (JsonTokenType.String or JsonTokenType.PropertyName))
        {
            ScalarHelpers.ThrowInvalidStringToken(reader.TokenType);
        }

        if (ScalarHelpers.GetValueLength(in reader) is < 1 or > 6)
        {
            return SerializationHelpers.ThrowInvalidOperationException<char>("Expected a single UTF-16 character.");
        }

        Span<char> buffer = stackalloc char[6];
        if (reader.CopyString(buffer) != 1)
        {
            return SerializationHelpers.ThrowInvalidOperationException<char>("Expected a single UTF-16 character.");
        }

        return buffer[0];
    }
    /// <inheritdoc/>
    public static void WriteAsPropertyName(Utf8JsonWriter writer, char value) => writer.WritePropertyName(new ReadOnlySpan<char>(in value));
    /// <inheritdoc/>
    public static char ReadAsPropertyName(ref Utf8JsonReader reader) => Read(ref reader);
}

/// <summary>Serializes strings and property names.</summary>
public sealed class StringConverter : IJsonSerializable<string>
{
    private StringConverter() { }
    /// <inheritdoc/>
    public static void Write(Utf8JsonWriter writer, string? value) => writer.WriteStringValue(value);
    /// <inheritdoc/>
    public static string? Read(ref Utf8JsonReader reader) => reader.GetString();
    /// <inheritdoc/>
    public static void WriteAsPropertyName(Utf8JsonWriter writer, string value) => writer.WritePropertyName(value);
    /// <inheritdoc/>
    public static string ReadAsPropertyName(ref Utf8JsonReader reader) => reader.GetString()!;
}

/// <summary>Serializes byte arrays as base64 strings.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Matches the shared source-generated identifier for byte[].")]
public sealed class Array_ByteConverter : IJsonSerializable<byte[]>
{
    private Array_ByteConverter() { }
    /// <inheritdoc/>
    public static void Write(Utf8JsonWriter writer, byte[]? value)
    {
        if (value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteBase64StringValue(value);
        }
    }
    /// <inheritdoc/>
    public static byte[]? Read(ref Utf8JsonReader reader) => reader.TokenType is JsonTokenType.Null ? null : reader.GetBytesFromBase64();
}

/// <summary>Serializes GUID values and property names.</summary>
public sealed class GuidConverter : IJsonSerializable<Guid>
{
    private GuidConverter() { }
    /// <inheritdoc/>
    public static void Write(Utf8JsonWriter writer, Guid value) => writer.WriteStringValue(value);
    /// <inheritdoc/>
    public static Guid Read(ref Utf8JsonReader reader) => reader.GetGuid();
    /// <inheritdoc/>
    public static void WriteAsPropertyName(Utf8JsonWriter writer, Guid value)
    {
        Span<byte> buffer = stackalloc byte[36];
        if (!value.TryFormat(buffer, out int written, "D"))
        {
            ScalarHelpers.ThrowFormattingException();
        }

        ScalarHelpers.WriteUnescapedPropertyName(writer, buffer[..written]);
    }
    /// <inheritdoc/>
    public static Guid ReadAsPropertyName(ref Utf8JsonReader reader)
    {
        Span<byte> buffer = stackalloc byte[218];
        int written = ScalarHelpers.CopyQuotedPropertyName(ref reader, buffer);
        Utf8JsonReader valueReader = new(buffer[..written]);
        valueReader.Read();

        return valueReader.GetGuid();
    }
}

/// <summary>Serializes date/time values and property names.</summary>
public sealed class DateTimeConverter : IJsonSerializable<DateTime>
{
    private DateTimeConverter() { }
    /// <inheritdoc/>
    public static void Write(Utf8JsonWriter writer, DateTime value) => writer.WriteStringValue(value);
    /// <inheritdoc/>
    public static DateTime Read(ref Utf8JsonReader reader) => reader.GetDateTime();
    /// <inheritdoc/>
    public static void WriteAsPropertyName(Utf8JsonWriter writer, DateTime value)
    {
        Span<byte> buffer = stackalloc byte[33];
        if (!value.TryFormat(buffer, out int written, "O", CultureInfo.InvariantCulture))
        {
            ScalarHelpers.ThrowFormattingException();
        }

        written = ScalarHelpers.TrimDateTimeFraction(buffer, written);
        ScalarHelpers.WriteUnescapedPropertyName(writer, buffer[..written]);
    }
    /// <inheritdoc/>
    public static DateTime ReadAsPropertyName(ref Utf8JsonReader reader)
    {
        Span<byte> buffer = stackalloc byte[254];
        int written = ScalarHelpers.CopyQuotedPropertyName(ref reader, buffer);
        Utf8JsonReader valueReader = new(buffer[..written]);
        valueReader.Read();

        return valueReader.GetDateTime();
    }
}

/// <summary>Serializes offset date/time values and property names.</summary>
public sealed class DateTimeOffsetConverter : IJsonSerializable<DateTimeOffset>
{
    private DateTimeOffsetConverter() { }
    /// <inheritdoc/>
    public static void Write(Utf8JsonWriter writer, DateTimeOffset value) => writer.WriteStringValue(value);
    /// <inheritdoc/>
    public static DateTimeOffset Read(ref Utf8JsonReader reader) => reader.GetDateTimeOffset();
    /// <inheritdoc/>
    public static void WriteAsPropertyName(Utf8JsonWriter writer, DateTimeOffset value)
    {
        Span<byte> buffer = stackalloc byte[33];
        if (!value.TryFormat(buffer, out int written, "O", CultureInfo.InvariantCulture))
        {
            ScalarHelpers.ThrowFormattingException();
        }

        written = ScalarHelpers.TrimDateTimeFraction(buffer, written);
        ScalarHelpers.WriteUnescapedPropertyName(writer, buffer[..written]);
    }
    /// <inheritdoc/>
    public static DateTimeOffset ReadAsPropertyName(ref Utf8JsonReader reader)
    {
        Span<byte> buffer = stackalloc byte[254];
        int written = ScalarHelpers.CopyQuotedPropertyName(ref reader, buffer);
        Utf8JsonReader valueReader = new(buffer[..written]);
        valueReader.Read();

        return valueReader.GetDateTimeOffset();
    }
}

/// <summary>Serializes time spans and property names.</summary>
public sealed class TimeSpanConverter : IJsonSerializable<TimeSpan>
{
    private TimeSpanConverter() { }
    /// <inheritdoc/>
    public static void Write(Utf8JsonWriter writer, TimeSpan value)
    {
        Span<byte> buffer = stackalloc byte[26];
        if (!Utf8Formatter.TryFormat(value, buffer, out int written, 'c'))
        {
            ScalarHelpers.ThrowFormattingException();
        }

        writer.WriteStringValue(buffer[..written]);
    }
    /// <inheritdoc/>
    public static TimeSpan Read(ref Utf8JsonReader reader)
    {
        if (reader.TokenType is not JsonTokenType.String)
        {
            ScalarHelpers.ThrowInvalidStringToken(reader.TokenType);
        }

        return ReadCore(ref reader);
    }
    /// <inheritdoc/>
    public static void WriteAsPropertyName(Utf8JsonWriter writer, TimeSpan value)
    {
        Span<byte> buffer = stackalloc byte[26];
        if (!Utf8Formatter.TryFormat(value, buffer, out int written, 'c'))
        {
            ScalarHelpers.ThrowFormattingException();
        }

        writer.WritePropertyName(buffer[..written]);
    }
    /// <inheritdoc/>
    public static TimeSpan ReadAsPropertyName(ref Utf8JsonReader reader) => ReadCore(ref reader);

    private static TimeSpan ReadCore(ref Utf8JsonReader reader)
    {
        if (ScalarHelpers.GetValueLength(in reader) is < 1 or > 156)
        {
            ScalarHelpers.ThrowFormatException();
        }

        Span<byte> buffer = stackalloc byte[156];
        byte[]? rented = null;
        ReadOnlySpan<byte> source = ScalarHelpers.GetUtf8Value(in reader, buffer, ref rented);
        TimeSpan result = default;
        if (source.IsEmpty || source[0] is not (>= (byte)'0' and <= (byte)'9' or (byte)'-') ||
            !Utf8Parser.TryParse(source, out result, out int consumed, 'c') || consumed != source.Length)
        {
            ScalarHelpers.ThrowFormatException();
        }

        return result;
    }
}

/// <summary>Serializes dates and property names.</summary>
public sealed class DateOnlyConverter : IJsonSerializable<DateOnly>
{
    private DateOnlyConverter() { }
    /// <inheritdoc/>
    public static void Write(Utf8JsonWriter writer, DateOnly value)
    {
        Span<byte> buffer = stackalloc byte[10];
        if (!value.TryFormat(buffer, out int written, "O", CultureInfo.InvariantCulture))
        {
            ScalarHelpers.ThrowFormattingException();
        }

        writer.WriteStringValue(buffer[..written]);
    }
    /// <inheritdoc/>
    public static DateOnly Read(ref Utf8JsonReader reader)
    {
        if (reader.TokenType is not JsonTokenType.String)
        {
            ScalarHelpers.ThrowInvalidStringToken(reader.TokenType);
        }

        return ReadCore(ref reader);
    }
    /// <inheritdoc/>
    public static void WriteAsPropertyName(Utf8JsonWriter writer, DateOnly value)
    {
        Span<byte> buffer = stackalloc byte[10];
        if (!value.TryFormat(buffer, out int written, "O", CultureInfo.InvariantCulture))
        {
            ScalarHelpers.ThrowFormattingException();
        }

        writer.WritePropertyName(buffer[..written]);
    }
    /// <inheritdoc/>
    public static DateOnly ReadAsPropertyName(ref Utf8JsonReader reader) => ReadCore(ref reader);

    private static DateOnly ReadCore(ref Utf8JsonReader reader)
    {
        if (ScalarHelpers.GetValueLength(in reader) is < 10 or > 60)
        {
            ScalarHelpers.ThrowFormatException();
        }

        Span<char> buffer = stackalloc char[60];
        int written = reader.CopyString(buffer);
        DateOnly result = default;
        if (written != 10 || !DateOnly.TryParseExact(buffer[..written], "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
        {
            ScalarHelpers.ThrowFormatException();
        }

        return result;
    }
}

/// <summary>Serializes times and property names.</summary>
public sealed class TimeOnlyConverter : IJsonSerializable<TimeOnly>
{
    private TimeOnlyConverter() { }
    /// <inheritdoc/>
    public static void Write(Utf8JsonWriter writer, TimeOnly value)
    {
        Span<byte> buffer = stackalloc byte[16];
        if (!Utf8Formatter.TryFormat(value.ToTimeSpan(), buffer, out int written, 'c'))
        {
            ScalarHelpers.ThrowFormattingException();
        }

        writer.WriteStringValue(buffer[..written]);
    }
    /// <inheritdoc/>
    public static TimeOnly Read(ref Utf8JsonReader reader)
    {
        if (reader.TokenType is not JsonTokenType.String)
        {
            ScalarHelpers.ThrowInvalidStringToken(reader.TokenType);
        }

        return ReadCore(ref reader);
    }
    /// <inheritdoc/>
    public static void WriteAsPropertyName(Utf8JsonWriter writer, TimeOnly value)
    {
        Span<byte> buffer = stackalloc byte[16];
        if (!Utf8Formatter.TryFormat(value.ToTimeSpan(), buffer, out int written, 'c'))
        {
            ScalarHelpers.ThrowFormattingException();
        }

        writer.WritePropertyName(buffer[..written]);
    }
    /// <inheritdoc/>
    public static TimeOnly ReadAsPropertyName(ref Utf8JsonReader reader) => ReadCore(ref reader);

    private static TimeOnly ReadCore(ref Utf8JsonReader reader)
    {
        if (ScalarHelpers.GetValueLength(in reader) is < 3 or > 96)
        {
            ScalarHelpers.ThrowFormatException();
        }

        Span<byte> buffer = stackalloc byte[96];
        byte[]? rented = null;
        ReadOnlySpan<byte> source = ScalarHelpers.GetUtf8Value(in reader, buffer, ref rented);
        int separator = source.IndexOfAny((byte)'.', (byte)':');
        TimeSpan value = default;
        if (source.IsEmpty || source[0] is not (>= (byte)'0' and <= (byte)'9') ||
            separator < 0 || source[separator] != (byte)':' ||
            !Utf8Parser.TryParse(source, out value, out int consumed, 'c') || consumed != source.Length ||
            value.Ticks < 0 || value.Ticks >= TimeSpan.TicksPerDay)
        {
            ScalarHelpers.ThrowFormatException();
        }

        return TimeOnly.FromTimeSpan(value);
    }
}

/// <summary>Serializes signed 128-bit integers and property names.</summary>
public sealed class Int128Converter : IJsonSerializable<Int128>
{
    private Int128Converter() { }
    /// <inheritdoc/>
    public static void Write(Utf8JsonWriter writer, Int128 value)
    {
        Span<byte> buffer = stackalloc byte[40];
        if (!value.TryFormat(buffer, out int written, provider: CultureInfo.InvariantCulture))
        {
            ScalarHelpers.ThrowFormattingException();
        }

        writer.WriteRawValue(buffer[..written]);
    }
    /// <inheritdoc/>
    public static Int128 Read(ref Utf8JsonReader reader)
    {
        if (reader.TokenType is not JsonTokenType.Number)
        {
            ScalarHelpers.ThrowInvalidNumberToken(reader.TokenType);
        }

        return ScalarHelpers.ReadNumberPropertyName<Int128>(ref reader, NumberStyles.Integer);
    }
    /// <inheritdoc/>
    public static void WriteAsPropertyName(Utf8JsonWriter writer, Int128 value) => ScalarHelpers.WriteNumberPropertyName(writer, value);
    /// <inheritdoc/>
    public static Int128 ReadAsPropertyName(ref Utf8JsonReader reader) => ScalarHelpers.ReadNumberPropertyName<Int128>(ref reader, NumberStyles.Integer);
}

/// <summary>Serializes unsigned 128-bit integers and property names.</summary>
public sealed class UInt128Converter : IJsonSerializable<UInt128>
{
    private UInt128Converter() { }
    /// <inheritdoc/>
    public static void Write(Utf8JsonWriter writer, UInt128 value)
    {
        Span<byte> buffer = stackalloc byte[39];
        if (!value.TryFormat(buffer, out int written, provider: CultureInfo.InvariantCulture))
        {
            ScalarHelpers.ThrowFormattingException();
        }

        writer.WriteRawValue(buffer[..written]);
    }
    /// <inheritdoc/>
    public static UInt128 Read(ref Utf8JsonReader reader)
    {
        if (reader.TokenType is not JsonTokenType.Number)
        {
            ScalarHelpers.ThrowInvalidNumberToken(reader.TokenType);
        }

        return ScalarHelpers.ReadNumberPropertyName<UInt128>(ref reader, NumberStyles.Integer);
    }
    /// <inheritdoc/>
    public static void WriteAsPropertyName(Utf8JsonWriter writer, UInt128 value) => ScalarHelpers.WriteNumberPropertyName(writer, value);
    /// <inheritdoc/>
    public static UInt128 ReadAsPropertyName(ref Utf8JsonReader reader) => ScalarHelpers.ReadNumberPropertyName<UInt128>(ref reader, NumberStyles.Integer);
}

/// <summary>Serializes half-precision numbers and property names.</summary>
public sealed class HalfConverter : IJsonSerializable<Half>
{
    private HalfConverter() { }
    /// <inheritdoc/>
    public static void Write(Utf8JsonWriter writer, Half value)
    {
        Span<byte> buffer = stackalloc byte[20];
        if (!value.TryFormat(buffer, out int written, provider: CultureInfo.InvariantCulture))
        {
            ScalarHelpers.ThrowFormattingException();
        }

        writer.WriteRawValue(buffer[..written]);
    }
    /// <inheritdoc/>
    public static Half Read(ref Utf8JsonReader reader)
    {
        if (reader.TokenType is not JsonTokenType.Number)
        {
            ScalarHelpers.ThrowInvalidNumberToken(reader.TokenType);
        }

        return ReadCore(ref reader);
    }
    /// <inheritdoc/>
    public static void WriteAsPropertyName(Utf8JsonWriter writer, Half value) => ScalarHelpers.WriteNumberPropertyName(writer, value);
    /// <inheritdoc/>
    public static Half ReadAsPropertyName(ref Utf8JsonReader reader) => ReadCore(ref reader);

    private static Half ReadCore(ref Utf8JsonReader reader)
    {
        Span<byte> buffer = stackalloc byte[ScalarHelpers.StackBufferSize];
        byte[]? rented = null;
        try
        {
            ReadOnlySpan<byte> source = ScalarHelpers.GetUtf8Value(in reader, buffer, ref rented);
            if (!Half.TryParse(source, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out Half result) ||
                Half.IsNaN(result) && !source.SequenceEqual("NaN"u8) ||
                Half.IsPositiveInfinity(result) && !source.SequenceEqual("Infinity"u8) ||
                Half.IsNegativeInfinity(result) && !source.SequenceEqual("-Infinity"u8))
            {
                ScalarHelpers.ThrowFormatException();
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
}

/// <summary>Serializes URI values and property names.</summary>
public sealed class UriConverter : IJsonSerializable<Uri>
{
    private UriConverter() { }
    /// <inheritdoc/>
    public static void Write(Utf8JsonWriter writer, Uri? value) => writer.WriteStringValue(value?.OriginalString);
    /// <inheritdoc/>
    public static Uri? Read(ref Utf8JsonReader reader) => reader.TokenType is JsonTokenType.Null ? null : ReadCore(ref reader);
    /// <inheritdoc/>
    public static void WriteAsPropertyName(Utf8JsonWriter writer, Uri value)
    {
        ArgumentNullException.ThrowIfNull(value);
        writer.WritePropertyName(value.OriginalString);
    }
    /// <inheritdoc/>
    public static Uri ReadAsPropertyName(ref Utf8JsonReader reader) => ReadCore(ref reader);

    private static Uri ReadCore(ref Utf8JsonReader reader)
    {
        if (!Uri.TryCreate(reader.GetString(), UriKind.RelativeOrAbsolute, out Uri? result))
        {
            return SerializationHelpers.ThrowJsonException<Uri>("Invalid URI.");
        }

        return result;
    }
}

/// <summary>Serializes version values and property names.</summary>
public sealed class VersionConverter : IJsonSerializable<Version>
{
    private VersionConverter() { }
    /// <inheritdoc/>
    public static void Write(Utf8JsonWriter writer, Version? value)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        Span<byte> buffer = stackalloc byte[43];
        if (!value.TryFormat(buffer, out int written))
        {
            ScalarHelpers.ThrowFormattingException();
        }

        writer.WriteStringValue(buffer[..written]);
    }
    /// <inheritdoc/>
    public static Version? Read(ref Utf8JsonReader reader)
    {
        if (reader.TokenType is JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType is not JsonTokenType.String)
        {
            ScalarHelpers.ThrowInvalidStringToken(reader.TokenType);
        }

        return ReadCore(ref reader);
    }
    /// <inheritdoc/>
    public static void WriteAsPropertyName(Utf8JsonWriter writer, Version value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Span<byte> buffer = stackalloc byte[43];
        if (!value.TryFormat(buffer, out int written))
        {
            ScalarHelpers.ThrowFormattingException();
        }

        writer.WritePropertyName(buffer[..written]);
    }
    /// <inheritdoc/>
    public static Version ReadAsPropertyName(ref Utf8JsonReader reader) => ReadCore(ref reader);

    private static Version ReadCore(ref Utf8JsonReader reader)
    {
        Span<byte> buffer = stackalloc byte[ScalarHelpers.StackBufferSize];
        byte[]? rented = null;
        try
        {
            ReadOnlySpan<byte> source = ScalarHelpers.GetUtf8Value(in reader, buffer, ref rented);
            if (source.IsEmpty || source[0] < 128 && char.IsWhiteSpace((char)source[0]) ||
                source[^1] < 128 && char.IsWhiteSpace((char)source[^1]))
            {
                ScalarHelpers.ThrowFormatException();
            }

            if (!Version.TryParse(source, out Version? result))
            {
                return SerializationHelpers.ThrowJsonException<Version>("Invalid version.");
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
}

/// <summary>Serializes arbitrary-precision integers as JSON numbers.</summary>
public sealed class BigIntegerConverter : IJsonSerializable<BigInteger>
{
    private BigIntegerConverter() { }

    /// <inheritdoc/>
    public static void Write(Utf8JsonWriter writer, BigInteger value) =>
        writer.WriteRawValue(value.ToString(CultureInfo.InvariantCulture));

    /// <inheritdoc/>
    public static BigInteger Read(ref Utf8JsonReader reader)
    {
        if (reader.TokenType is not JsonTokenType.Number)
        {
            SerializationHelpers.ThrowJsonException("Expected a JSON number.");
        }

        string text = reader.HasValueSequence
            ? Encoding.UTF8.GetString(reader.ValueSequence.ToArray())
            : Encoding.UTF8.GetString(reader.ValueSpan);
        if (!BigInteger.TryParse(text, CultureInfo.InvariantCulture, out BigInteger result))
        {
            SerializationHelpers.ThrowJsonException("Invalid arbitrary-precision integer.");
        }

        return result;
    }
}

/// <summary>Serializes Unicode scalar values as JSON strings.</summary>
public sealed class RuneConverter : IJsonSerializable<Rune>
{
    private RuneConverter() { }

    /// <inheritdoc/>
    public static void Write(Utf8JsonWriter writer, Rune value) => writer.WriteStringValue(value.ToString());

    /// <inheritdoc/>
    public static Rune Read(ref Utf8JsonReader reader)
    {
        if (reader.TokenType is not JsonTokenType.String ||
            reader.GetString() is not { } text ||
            !Rune.TryGetRuneAt(text, 0, out Rune result) ||
            text.Length != result.Utf16SequenceLength)
        {
            return SerializationHelpers.ThrowJsonException<Rune>("Expected a single Unicode scalar value.");
        }

        return result;
    }
}
