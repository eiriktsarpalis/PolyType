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
        bool hasEntries = false;
        foreach (KeyValuePair<TKey, TValue> entry in _getEnumerable(value))
        {
            if (!hasEntries)
            {
                writer.BeginSequence();
                hasEntries = true;
            }

            writer.WriteSequenceEntry();
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

        if (hasEntries)
        {
            writer.EndSequence();
        }
        else
        {
            writer.WriteRawScalar("[]");
        }
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

        if (reader.TryReadEmptyCollection())
        {
            return createObject();
        }

        TDictionary result = createObject();
        int expectedIndent = reader.CurrentIndent;

        while (reader.TryReadSequenceEntry(expectedIndent, out _))
        {
            int entryIndent = reader.CurrentIndent;
            TKey? key = default;
            TValue? val = default;

            while (reader.TryReadMappingEntry(entryIndent, out string mappingKey, out string? inlineValue))
            {
                if (mappingKey == "key")
                {
                    if (inlineValue is not null)
                    {
                        var inlineReader = new YamlReader(inlineValue);
                        key = _keyConverter.Read(inlineReader);
                    }
                    else
                    {
                        key = _keyConverter.Read(reader);
                    }
                }
                else if (mappingKey == "value")
                {
                    if (inlineValue is not null)
                    {
                        var inlineReader = new YamlReader(inlineValue);
                        val = _valueConverter.Read(inlineReader);
                    }
                    else
                    {
                        val = _valueConverter.Read(reader);
                    }
                }
            }

            _inserter(ref result, key!, val!);
        }

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

        if (reader.TryReadEmptyCollection())
        {
            return constructor([]);
        }

        using PooledList<KeyValuePair<TKey, TValue>> buffer = new();
        int expectedIndent = reader.CurrentIndent;

        while (reader.TryReadSequenceEntry(expectedIndent, out _))
        {
            int entryIndent = reader.CurrentIndent;
            TKey? key = default;
            TValue? val = default;

            while (reader.TryReadMappingEntry(entryIndent, out string mappingKey, out string? inlineValue))
            {
                if (mappingKey == "key")
                {
                    if (inlineValue is not null)
                    {
                        var inlineReader = new YamlReader(inlineValue);
                        key = _keyConverter.Read(inlineReader);
                    }
                    else
                    {
                        key = _keyConverter.Read(reader);
                    }
                }
                else if (mappingKey == "value")
                {
                    if (inlineValue is not null)
                    {
                        var inlineReader = new YamlReader(inlineValue);
                        val = _valueConverter.Read(inlineReader);
                    }
                    else
                    {
                        val = _valueConverter.Read(reader);
                    }
                }
            }

            buffer.Add(new(key!, val!));
        }

        return constructor(buffer.AsSpan());
    }
}
