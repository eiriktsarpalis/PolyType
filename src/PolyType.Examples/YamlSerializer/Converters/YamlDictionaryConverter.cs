using PolyType.Abstractions;
using PolyType.Examples.Utilities;

namespace PolyType.Examples.YamlSerializer.Converters;

internal class YamlDictionaryConverter<TDictionary, TKey, TValue>(
    YamlConverter<TKey> keyConverter,
    YamlConverter<TValue> valueConverter,
    Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> getEnumerable)
    : YamlConverter<TDictionary>
    where TKey : notnull
{
    private protected readonly YamlConverter<TKey> _keyConverter = keyConverter;
    private protected readonly YamlConverter<TValue> _valueConverter = valueConverter;
    private readonly Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> _getEnumerable = getEnumerable;

    public override TDictionary? Read(YamlReader reader)
    {
        if (default(TDictionary) is null && reader.TryReadNull())
        {
            return default;
        }

        throw new NotSupportedException($"Type {typeof(TDictionary)} does not support deserialization.");
    }

    public sealed override void Write(YamlWriter writer, TDictionary value)
    {
        writer.BeginSequence();
        foreach (KeyValuePair<TKey, TValue> entry in _getEnumerable(value))
        {
            writer.BeginMapping();
            writer.WriteKey("key");
            _keyConverter.Write(writer, entry.Key);
            writer.WriteKey("value");
            if (entry.Value is null)
            {
                writer.WriteNull();
            }
            else
            {
                _valueConverter.Write(writer, entry.Value);
            }

            writer.EndMapping();
        }

        writer.EndSequence();
    }
}

internal sealed class YamlMutableDictionaryConverter<TDictionary, TKey, TValue>(
    YamlConverter<TKey> keyConverter,
    YamlConverter<TValue> valueConverter,
    Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> getEnumerable,
    MutableCollectionConstructor<TKey, TDictionary> createObject,
    DictionaryInserter<TDictionary, TKey, TValue> inserter)
    : YamlDictionaryConverter<TDictionary, TKey, TValue>(keyConverter, valueConverter, getEnumerable)
    where TKey : notnull
{
    private readonly DictionaryInserter<TDictionary, TKey, TValue> _inserter = inserter;

    public override TDictionary? Read(YamlReader reader)
    {
        if (default(TDictionary) is null && reader.TryReadNull())
        {
            return default;
        }

        reader.ReadSequenceStart();
        TDictionary result = createObject();

        while (!reader.IsSequenceEnd)
        {
            reader.ReadMappingStart();
            TKey? key = default;
            TValue? val = default;

            while (reader.TryReadMappingKey(out string mappingKey))
            {
                if (mappingKey == "key")
                {
                    key = _keyConverter.Read(reader);
                }
                else if (mappingKey == "value")
                {
                    val = _valueConverter.Read(reader);
                }
                else
                {
                    reader.SkipValue();
                }
            }

            reader.ReadMappingEnd();
            _inserter(ref result, key!, val!);
        }

        reader.ReadSequenceEnd();

        return result;
    }
}

internal sealed class YamlParameterizedDictionaryConverter<TDictionary, TKey, TValue>(
    YamlConverter<TKey> keyConverter,
    YamlConverter<TValue> valueConverter,
    Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> getEnumerable,
    ParameterizedCollectionConstructor<TKey, KeyValuePair<TKey, TValue>, TDictionary> constructor)
    : YamlDictionaryConverter<TDictionary, TKey, TValue>(keyConverter, valueConverter, getEnumerable)
    where TKey : notnull
{
    public sealed override TDictionary? Read(YamlReader reader)
    {
        if (default(TDictionary) is null && reader.TryReadNull())
        {
            return default;
        }

        reader.ReadSequenceStart();
        using PooledList<KeyValuePair<TKey, TValue>> buffer = new();

        while (!reader.IsSequenceEnd)
        {
            reader.ReadMappingStart();
            TKey? key = default;
            TValue? val = default;

            while (reader.TryReadMappingKey(out string mappingKey))
            {
                if (mappingKey == "key")
                {
                    key = _keyConverter.Read(reader);
                }
                else if (mappingKey == "value")
                {
                    val = _valueConverter.Read(reader);
                }
                else
                {
                    reader.SkipValue();
                }
            }

            reader.ReadMappingEnd();
            buffer.Add(new(key!, val!));
        }

        reader.ReadSequenceEnd();

        return constructor(buffer.AsSpan());
    }
}