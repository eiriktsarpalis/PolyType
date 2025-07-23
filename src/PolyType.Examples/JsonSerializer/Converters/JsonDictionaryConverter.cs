using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using PolyType.Abstractions;
using PolyType.Examples.Utilities;

namespace PolyType.Examples.JsonSerializer.Converters;

internal class JsonDictionaryConverter<TDictionary, TKey, TValue>(
    JsonConverter<TKey> keyConverter,
    JsonConverter<TValue> valueConverter,
    IDictionaryTypeShape<TDictionary, TKey, TValue> shape)
    : JsonConverter<TDictionary>, IJsonObjectConverter<TDictionary>
    where TKey : notnull
{
    private static readonly bool s_isDictionary = typeof(Dictionary<TKey, TValue>).IsAssignableFrom(typeof(TDictionary));
    private protected readonly JsonConverter<TKey> _keyConverter = keyConverter;
    private protected readonly JsonConverter<TValue> _valueConverter = valueConverter;
    private readonly Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> _getDictionary = shape.GetGetDictionary();

    public override TDictionary? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (default(TDictionary) is null && reader.TokenType is JsonTokenType.Null)
        {
            return default;
        }

        throw new NotSupportedException($"Type {typeof(TDictionary)} does not support deserialization.");
    }

    public sealed override void Write(Utf8JsonWriter writer, TDictionary value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();
        WriteProperties(writer, value, options);
        writer.WriteEndObject();
    }

    public void WriteProperties(Utf8JsonWriter writer, TDictionary value, JsonSerializerOptions options)
    {
        DebugExt.Assert(value is not null);

        if (s_isDictionary)
        {
            WriteEntriesAsDictionary(writer, (Dictionary<TKey, TValue>)(object)value, options);
        }
        else
        {
            WriteEntriesAsReadOnlyDictionary(writer, value, options);
        }
    }

    private void WriteEntriesAsDictionary(Utf8JsonWriter writer, Dictionary<TKey, TValue> value, JsonSerializerOptions options)
    {
        JsonConverter<TKey> keyConverter = _keyConverter;
        JsonConverter<TValue> valueConverter = _valueConverter;

        foreach (KeyValuePair<TKey, TValue> entry in value)
        {
            keyConverter.WriteAsPropertyName(writer, entry.Key, options);
            valueConverter.Write(writer, entry.Value, options);
        }
    }

    private void WriteEntriesAsReadOnlyDictionary(Utf8JsonWriter writer, TDictionary value, JsonSerializerOptions options)
    {
        JsonConverter<TKey> keyConverter = _keyConverter;
        JsonConverter<TValue> valueConverter = _valueConverter;

        foreach (KeyValuePair<TKey, TValue> entry in _getDictionary(value))
        {
            keyConverter.WriteAsPropertyName(writer, entry.Key, options);
            valueConverter.Write(writer, entry.Value, options);
        }
    }
}

internal sealed class JsonMutableDictionaryConverter<TDictionary, TKey, TValue>(
    JsonConverter<TKey> keyConverter,
    JsonConverter<TValue> valueConverter,
    IDictionaryTypeShape<TDictionary, TKey, TValue> shape,
    MutableCollectionConstructor<TKey, TDictionary> createObject,
    DictionaryInserter<TDictionary, TKey, TValue> inserter) : JsonDictionaryConverter<TDictionary, TKey, TValue>(keyConverter, valueConverter, shape)
    where TKey : notnull
{
    private readonly DictionaryInserter<TDictionary, TKey, TValue> _inserter = inserter;

    public override TDictionary? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (default(TDictionary) is null && reader.TokenType is JsonTokenType.Null)
        {
            return default;
        }

        reader.EnsureTokenType(JsonTokenType.StartObject);

        TDictionary result = createObject();
        reader.EnsureRead();

        JsonConverter<TKey> keyConverter = _keyConverter;
        JsonConverter<TValue> valueConverter = _valueConverter;
        DictionaryInserter<TDictionary, TKey, TValue> inserter = _inserter;

        while (reader.TokenType != JsonTokenType.EndObject)
        {
            Debug.Assert(reader.TokenType == JsonTokenType.PropertyName);

            if (reader.ValueSpan.SequenceEqual(JsonHelpers.DiscriminatorPropertyName))
            {
                reader.EnsureRead();
                reader.Skip();
                reader.EnsureRead();
                continue;
            }

            TKey key = keyConverter.ReadAsPropertyName(ref reader, typeof(TKey), options);
            reader.EnsureRead();
            TValue value = valueConverter.Read(ref reader, typeof(TValue), options)!;
            reader.EnsureRead();

            inserter(ref result, key, value);
        }

        return result;
    }
}

internal sealed class JsonParameterizedDictionaryConverter<TDictionary, TKey, TValue>(
    JsonConverter<TKey> keyConverter,
    JsonConverter<TValue> valueConverter,
    IDictionaryTypeShape<TDictionary, TKey, TValue> shape,
    ParameterizedCollectionConstructor<TKey, KeyValuePair<TKey, TValue>, TDictionary> constructor)
    : JsonDictionaryConverter<TDictionary, TKey, TValue>(keyConverter, valueConverter, shape)
    where TKey : notnull
{
    public sealed override TDictionary? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (default(TDictionary) is null && reader.TokenType is JsonTokenType.Null)
        {
            return default;
        }

        reader.EnsureTokenType(JsonTokenType.StartObject);

        using PooledList<KeyValuePair<TKey, TValue>> buffer = new();
        reader.EnsureRead();

        JsonConverter<TKey> keyConverter = _keyConverter;
        JsonConverter<TValue> valueConverter = _valueConverter;

        while (reader.TokenType != JsonTokenType.EndObject)
        {
            Debug.Assert(reader.TokenType == JsonTokenType.PropertyName);

            TKey key = keyConverter.ReadAsPropertyName(ref reader, typeof(TKey), options);
            reader.EnsureRead();
            TValue value = valueConverter.Read(ref reader, typeof(TValue), options)!;
            reader.EnsureRead();
            buffer.Add(new(key, value));
        }

        return constructor(buffer.AsSpan());
    }
}