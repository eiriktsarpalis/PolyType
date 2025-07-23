using PolyType.Abstractions;
using PolyType.Examples.Utilities;
using System.Xml;

namespace PolyType.Examples.XmlSerializer.Converters;

internal class XmlDictionaryConverter<TDictionary, TKey, TValue>(
    XmlConverter<TKey> keyConverter, 
    XmlConverter<TValue> valueConverter, 
    Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> getEnumerable) 
    : XmlConverter<TDictionary>
    where TKey : notnull
{
    private protected readonly XmlConverter<TKey> _keyConverter = keyConverter;
    private protected readonly XmlConverter<TValue> _valueConverter = valueConverter;
    private readonly Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> _getEnumerable = getEnumerable;

    public override TDictionary? Read(XmlReader reader)
    {
        if (default(TDictionary) is null && reader.TryReadNullElement())
        {
            return default;
        }
        
        throw new NotSupportedException($"Type {typeof(TDictionary)} does not support deserialization.");
    }

    public sealed override void Write(XmlWriter writer, TDictionary value)
    {
        XmlConverter<TKey> keyConverter = _keyConverter;
        XmlConverter<TValue> valueConverter = _valueConverter;

        foreach (KeyValuePair<TKey, TValue> entry in _getEnumerable(value))
        {
            writer.WriteStartElement("entry");
            keyConverter.WriteAsElement(writer, "key", entry.Key);
            valueConverter.WriteAsElement(writer, "value", entry.Value);
            writer.WriteEndElement();
        }
    }
}

internal sealed class XmlMutableDictionaryConverter<TDictionary, TKey, TValue>(
    XmlConverter<TKey> keyConverter,
    XmlConverter<TValue> valueConverter,
    Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> getEnumerable,
    MutableCollectionConstructor<TKey, TDictionary> createObject,
    DictionaryInserter<TDictionary, TKey, TValue> inserter)
    : XmlDictionaryConverter<TDictionary, TKey, TValue>(keyConverter, valueConverter, getEnumerable)
    where TKey : notnull
{
    private readonly DictionaryInserter<TDictionary, TKey, TValue> _inserter = inserter;

    public override TDictionary? Read(XmlReader reader)
    {
        if (default(TDictionary) is null && reader.TryReadNullElement())
        {
            return default;
        }

        TDictionary result = createObject();

        if (reader.IsEmptyElement)
        {
            reader.Read();
            return result;
        }

        XmlConverter<TKey> keyConverter = _keyConverter;
        XmlConverter<TValue> valueConverter = _valueConverter;
        DictionaryInserter<TDictionary, TKey, TValue> inserter = _inserter;
        
        reader.ReadStartElement();
        while (reader.NodeType != XmlNodeType.EndElement)
        {
            if (reader.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            reader.ReadStartElement();
            TKey key = keyConverter.Read(reader)!;
            TValue value = valueConverter.Read(reader)!;
            inserter(ref result, key, value);
            reader.ReadEndElement();
        }

        reader.ReadEndElement();
        return result;
    }
}

internal sealed class XmlParameterizedDictionaryConverter<TDictionary, TKey, TValue>(
    XmlConverter<TKey> keyConverter,
    XmlConverter<TValue> valueConverter,
    Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> getEnumerable,
    ParameterizedCollectionConstructor<TKey, KeyValuePair<TKey, TValue>, TDictionary> constructor)
    : XmlDictionaryConverter<TDictionary, TKey, TValue>(keyConverter, valueConverter, getEnumerable)
    where TKey : notnull
{
    public sealed override TDictionary? Read(XmlReader reader)
    {
        if (default(TDictionary) is null && reader.TryReadNullElement())
        {
            return default;
        }

        using PooledList<KeyValuePair<TKey, TValue>> buffer = new();

        if (reader.IsEmptyElement)
        {
            reader.Read();
            return constructor(buffer.AsSpan());
        }

        XmlConverter<TKey> keyConverter = _keyConverter;
        XmlConverter<TValue> valueConverter = _valueConverter;

        reader.ReadStartElement();
        while (reader.NodeType != XmlNodeType.EndElement)
        {
            if (reader.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            reader.ReadStartElement();
            TKey key = keyConverter.Read(reader)!;
            TValue value = valueConverter.Read(reader)!;
            buffer.Add(new(key, value));
            reader.ReadEndElement();
        }

        reader.ReadEndElement();
        return constructor(buffer.AsSpan());
    }
}