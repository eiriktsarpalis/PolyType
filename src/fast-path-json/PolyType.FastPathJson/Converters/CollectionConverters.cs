using System.Text.Json;

namespace PolyType.FastPathJson.Converters;

/// <summary>Composes an element witness into a nullable-value converter.</summary>
/// <typeparam name="T">The underlying value type.</typeparam>
/// <typeparam name="TConverter">The underlying converter witness.</typeparam>
public sealed class NullableConverter<T, TConverter> : IJsonSerializable<T?>
    where T : struct
    where TConverter : IJsonSerializable<T>
{
    private NullableConverter() { }

    /// <inheritdoc/>
    public static void Write(Utf8JsonWriter writer, T? value)
    {
        if (value is { } element)
        {
            TConverter.Write(writer, element);
        }
        else
        {
            writer.WriteNullValue();
        }
    }

    /// <inheritdoc/>
    public static T? Read(ref Utf8JsonReader reader) =>
        reader.TokenType is JsonTokenType.Null ? null : TConverter.Read(ref reader);
}

/// <summary>Composes an element witness into a list converter.</summary>
/// <typeparam name="T">The element type.</typeparam>
/// <typeparam name="TConverter">The element converter witness.</typeparam>
public sealed class ListConverter<T, TConverter> : IJsonSerializable<List<T>>
    where TConverter : IJsonSerializable<T>
{
    private ListConverter() { }

    /// <inheritdoc/>
    public static void Write(Utf8JsonWriter writer, List<T>? value)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartArray();
        foreach (T element in value)
        {
            TConverter.Write(writer, element);
        }

        writer.WriteEndArray();
    }

    /// <inheritdoc/>
    public static List<T>? Read(ref Utf8JsonReader reader)
    {
        if (reader.TokenType is JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType is not JsonTokenType.StartArray)
        {
            SerializationHelpers.ThrowJsonException("Expected a JSON array.");
        }

        List<T> result = [];
        while (reader.Read())
        {
            if (reader.TokenType is JsonTokenType.EndArray)
            {
                return result;
            }

            result.Add(TConverter.Read(ref reader)!);
        }

        return SerializationHelpers.ThrowJsonException<List<T>>("Incomplete JSON array.");
    }
}

/// <summary>Composes an element witness into a one-dimensional array converter.</summary>
/// <typeparam name="T">The element type.</typeparam>
/// <typeparam name="TConverter">The element converter witness.</typeparam>
public sealed class ArrayConverter<T, TConverter> : IJsonSerializable<T[]>
    where TConverter : IJsonSerializable<T>
{
    private ArrayConverter() { }

    /// <inheritdoc/>
    public static void Write(Utf8JsonWriter writer, T[]? value)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartArray();
        foreach (T element in value)
        {
            TConverter.Write(writer, element);
        }

        writer.WriteEndArray();
    }

    /// <inheritdoc/>
    public static T[]? Read(ref Utf8JsonReader reader) =>
        ListConverter<T, TConverter>.Read(ref reader)?.ToArray();
}

/// <summary>Composes key and value witnesses into a dictionary converter.</summary>
/// <typeparam name="TKey">The key type.</typeparam>
/// <typeparam name="TValue">The value type.</typeparam>
/// <typeparam name="TKeyConverter">The key converter, including property-name support.</typeparam>
/// <typeparam name="TValueConverter">The value converter.</typeparam>
public sealed class DictionaryConverter<TKey, TValue, TKeyConverter, TValueConverter> : IJsonSerializable<Dictionary<TKey, TValue>>
    where TKey : notnull
    where TKeyConverter : IJsonSerializable<TKey>
    where TValueConverter : IJsonSerializable<TValue>
{
    private DictionaryConverter() { }

    /// <inheritdoc/>
    public static void Write(Utf8JsonWriter writer, Dictionary<TKey, TValue>? value)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();
        foreach ((TKey key, TValue element) in value)
        {
            TKeyConverter.WriteAsPropertyName(writer, key);
            TValueConverter.Write(writer, element);
        }

        writer.WriteEndObject();
    }

    /// <inheritdoc/>
    public static Dictionary<TKey, TValue>? Read(ref Utf8JsonReader reader)
    {
        if (reader.TokenType is JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType is not JsonTokenType.StartObject)
        {
            SerializationHelpers.ThrowJsonException("Expected a JSON object.");
        }

        Dictionary<TKey, TValue> result = [];
        while (reader.Read())
        {
            if (reader.TokenType is JsonTokenType.EndObject)
            {
                return result;
            }

            bool isDiscriminator = reader.ValueTextEquals("$type"u8);
            TKey key = isDiscriminator ? default! : TKeyConverter.ReadAsPropertyName(ref reader);
            if (!reader.Read())
            {
                SerializationHelpers.ThrowJsonException("Incomplete JSON object.");
            }

            if (isDiscriminator)
            {
                reader.Skip();
            }
            else
            {
                result[key] = TValueConverter.Read(ref reader)!;
            }
        }

        return SerializationHelpers.ThrowJsonException<Dictionary<TKey, TValue>>("Incomplete JSON object.");
    }
}
