// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PolyType.FastPathJson.Converters;

/// <summary>Serializes enums using System.Text.Json's string-enum representation.</summary>
/// <typeparam name="T">The enum type.</typeparam>
public sealed class EnumConverter<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] T> : IJsonSerializable<T>
    where T : struct, Enum
{
    private const int NameCacheLimit = 64;
    private static readonly TypeCode s_typeCode = Type.GetTypeCode(typeof(T));
    private static readonly bool s_isSigned = ((int)s_typeCode & 1) != 0;
    private static readonly bool s_isFlags = typeof(T).IsDefined(typeof(FlagsAttribute), inherit: false);
    private static readonly Field[] s_fields = GetFields();
    private static readonly ConcurrentDictionary<ulong, Name> s_names = new(
        s_fields.Select(field => new KeyValuePair<ulong, Name>(field.Bits, new(field.Text, JsonEncodedText.Encode(field.Text))))
            .DistinctBy(entry => entry.Key));
    private static readonly Dictionary<string, ulong> s_exactNames = GetExactNames();

    private EnumConverter() { }

    /// <inheritdoc/>
    public static void Write(Utf8JsonWriter writer, T value)
    {
        ulong bits = ToUInt64(value);
        if (TryGetName(bits, out Name name))
        {
            if (SerializationHelpers.UsesDefaultEncoder(writer))
            {
                writer.WriteStringValue(name.Encoded);
            }
            else
            {
                writer.WriteStringValue(name.Text);
            }
        }
        else if (s_isSigned)
        {
            writer.WriteNumberValue(ToInt64(value));
        }
        else
        {
            writer.WriteNumberValue(bits);
        }
    }

    /// <inheritdoc/>
    public static T Read(ref Utf8JsonReader reader)
    {
        if (reader.TokenType is JsonTokenType.String)
        {
            return ReadName(ref reader);
        }

        if (reader.TokenType is JsonTokenType.Number)
        {
            switch (s_typeCode)
            {
                case TypeCode.SByte when reader.TryGetSByte(out sbyte int8): return (T)(object)int8;
                case TypeCode.Byte when reader.TryGetByte(out byte uint8): return (T)(object)uint8;
                case TypeCode.Int16 when reader.TryGetInt16(out short int16): return (T)(object)int16;
                case TypeCode.UInt16 when reader.TryGetUInt16(out ushort uint16): return (T)(object)uint16;
                case TypeCode.Int32 when reader.TryGetInt32(out int int32): return (T)(object)int32;
                case TypeCode.UInt32 when reader.TryGetUInt32(out uint uint32): return (T)(object)uint32;
                case TypeCode.Int64 when reader.TryGetInt64(out long int64): return (T)(object)int64;
                case TypeCode.UInt64 when reader.TryGetUInt64(out ulong uint64): return (T)(object)uint64;
            }
        }

        return SerializationHelpers.ThrowJsonException<T>("Invalid enum value.");
    }

    /// <inheritdoc/>
    public static void WriteAsPropertyName(Utf8JsonWriter writer, T value)
    {
        ulong bits = ToUInt64(value);
        if (TryGetName(bits, out Name name))
        {
            if (SerializationHelpers.UsesDefaultEncoder(writer))
            {
                writer.WritePropertyName(name.Encoded);
            }
            else
            {
                writer.WritePropertyName(name.Text);
            }
        }
        else if (s_isSigned)
        {
            ScalarHelpers.WriteNumberPropertyName(writer, ToInt64(value));
        }
        else
        {
            ScalarHelpers.WriteNumberPropertyName(writer, bits);
        }
    }

    /// <inheritdoc/>
    public static T ReadAsPropertyName(ref Utf8JsonReader reader) => ReadName(ref reader);

    private static T ReadName(ref Utf8JsonReader reader)
    {
        int length = ScalarHelpers.GetValueLength(in reader);
        char[]? rented = null;
        try
        {
            Span<char> buffer = length <= ScalarHelpers.StackBufferSize
                ? stackalloc char[ScalarHelpers.StackBufferSize]
                : (rented = ArrayPool<char>.Shared.Rent(length));
            int written = reader.CopyString(buffer);
            ReadOnlySpan<char> source = buffer[..written].Trim();
            if (s_exactNames.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(source, out ulong exact))
            {
                return FromUInt64(exact);
            }

            if (IsInteger(source))
            {
                return Enum.TryParse(source, out T result)
                    ? result
                    : SerializationHelpers.ThrowJsonException<T>("Invalid enum value.");
            }

            ulong bits = 0;
            do
            {
                int separator = source.IndexOf(',');
                ReadOnlySpan<char> next = separator < 0 ? source : source[..separator].TrimEnd();
                source = separator < 0 ? default : source[(separator + 1)..].TrimStart();
                bool matched = false;
                foreach (Field field in s_fields)
                {
                    if (next.Equals(field.Text.AsSpan(), field.IsAttribute ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase))
                    {
                        bits |= field.Bits;
                        matched = true;
                        break;
                    }
                }

                if (!matched)
                {
                    return SerializationHelpers.ThrowJsonException<T>("Invalid enum value.");
                }
            }
            while (!source.IsEmpty);

            return FromUInt64(bits);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<char>.Shared.Return(rented, clearArray: true);
            }
        }
    }

    private static bool TryGetName(ulong bits, out Name name)
    {
        if (s_names.TryGetValue(bits, out name))
        {
            return true;
        }

        if (!s_isFlags || !IsFlagCombination(bits))
        {
            return false;
        }

        StringBuilder builder = new();
        ulong remaining = bits;
        foreach (Field field in s_fields)
        {
            if (field.Bits == 0 ? bits == 0 : (remaining & field.Bits) == field.Bits)
            {
                if (builder.Length > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(field.Text);
                remaining &= ~field.Bits;
                if (remaining == 0)
                {
                    break;
                }
            }
        }

        string text = builder.ToString();
        name = new(text, JsonEncodedText.Encode(text));
        if (s_names.Count < NameCacheLimit)
        {
            s_names.TryAdd(bits, name);
        }

        return true;
    }

    private static bool IsFlagCombination(ulong bits)
    {
        ulong remaining = bits;
        foreach (Field field in s_fields)
        {
            if (field.Bits == 0 ? bits == 0 : (remaining & field.Bits) == field.Bits)
            {
                remaining &= ~field.Bits;
                if (remaining == 0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static Field[] GetFields()
    {
        string[] names = Enum.GetNames<T>();
        T[] values = Enum.GetValues<T>();
        Field[] fields = new Field[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            JsonStringEnumMemberNameAttribute? attribute = typeof(T).GetField(names[i])!
                .GetCustomAttribute<JsonStringEnumMemberNameAttribute>();
            string text = attribute?.Name ?? names[i];
            if (text.Length > 0 && (char.IsWhiteSpace(text[0]) || char.IsWhiteSpace(text[^1])) ||
                s_isFlags && (text.Length == 0 || text.Contains(',')))
            {
                return SerializationHelpers.ThrowInvalidOperationException<Field[]>($"Invalid JSON enum name '{text}' for '{typeof(T)}'.");
            }

            fields[i] = new(ToUInt64(values[i]), text, attribute is not null);
        }

        return s_isFlags ? fields.OrderByDescending(field => BitOperations.PopCount(field.Bits)).ToArray() : fields;
    }

    private static Dictionary<string, ulong> GetExactNames()
    {
        Dictionary<string, ulong> names = new(StringComparer.Ordinal);
        foreach (Field field in s_fields)
        {
            names.TryAdd(field.Text, field.Bits);
        }

        return names;
    }

    private static bool IsInteger(ReadOnlySpan<char> source)
    {
        if (!source.IsEmpty && source[0] is '+' or '-')
        {
            source = source[1..];
        }

        return !source.IsEmpty && source.IndexOfAnyExceptInRange('0', '9') < 0;
    }

    private static ulong ToUInt64(T value) => s_typeCode switch
    {
        TypeCode.SByte => unchecked((ulong)(sbyte)(object)value),
        TypeCode.Byte => (byte)(object)value,
        TypeCode.Int16 => unchecked((ulong)(short)(object)value),
        TypeCode.UInt16 => (ushort)(object)value,
        TypeCode.Int32 => unchecked((ulong)(int)(object)value),
        TypeCode.UInt32 => (uint)(object)value,
        TypeCode.Int64 => unchecked((ulong)(long)(object)value),
        TypeCode.UInt64 => (ulong)(object)value,
        _ => SerializationHelpers.ThrowInvalidOperationException<ulong>("Unsupported enum underlying type."),
    };

    private static long ToInt64(T value) => s_typeCode switch
    {
        TypeCode.SByte => (sbyte)(object)value,
        TypeCode.Int16 => (short)(object)value,
        TypeCode.Int32 => (int)(object)value,
        TypeCode.Int64 => (long)(object)value,
        _ => SerializationHelpers.ThrowInvalidOperationException<long>("Unsupported signed enum underlying type."),
    };

    private static T FromUInt64(ulong value) => s_typeCode switch
    {
        TypeCode.SByte => (T)(object)unchecked((sbyte)value),
        TypeCode.Byte => (T)(object)unchecked((byte)value),
        TypeCode.Int16 => (T)(object)unchecked((short)value),
        TypeCode.UInt16 => (T)(object)unchecked((ushort)value),
        TypeCode.Int32 => (T)(object)unchecked((int)value),
        TypeCode.UInt32 => (T)(object)unchecked((uint)value),
        TypeCode.Int64 => (T)(object)unchecked((long)value),
        TypeCode.UInt64 => (T)(object)value,
        _ => SerializationHelpers.ThrowInvalidOperationException<T>("Unsupported enum underlying type."),
    };

    private sealed record Field(ulong Bits, string Text, bool IsAttribute);
    private readonly record struct Name(string Text, JsonEncodedText Encoded);
}
