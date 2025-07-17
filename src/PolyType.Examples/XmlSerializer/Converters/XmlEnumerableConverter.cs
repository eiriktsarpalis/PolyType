using PolyType.Abstractions;
using PolyType.Examples.Utilities;
using System.Xml;

namespace PolyType.Examples.XmlSerializer.Converters;

internal class XmlEnumerableConverter<TEnumerable, TElement>(
    XmlConverter<TElement> elementConverter,
    Func<TEnumerable, IEnumerable<TElement>> getEnumerable)
    : XmlConverter<TEnumerable>
{
    private protected readonly XmlConverter<TElement> _elementConverter = elementConverter;
    private readonly Func<TEnumerable, IEnumerable<TElement>> _getEnumerable = getEnumerable;

    public override TEnumerable? Read(XmlReader reader)
    {
        if (default(TEnumerable) is null && reader.TryReadNullElement())
        {
            return default;
        }

        throw new NotSupportedException($"Deserialization not supported for type {typeof(TEnumerable)}.");
    }

    public override void Write(XmlWriter writer, TEnumerable value)
    {
        XmlConverter<TElement> converter = _elementConverter;
        foreach (TElement element in _getEnumerable(value))
        {
            _elementConverter.WriteAsElement(writer, "element", element);
        }
    }
}

internal sealed class XmlMutableEnumerableConverter<TEnumerable, TElement>(
    XmlConverter<TElement> elementConverter,
    Func<TEnumerable, IEnumerable<TElement>> getEnumerable,
    MutableCollectionConstructor<TElement, TEnumerable> createObject,
    Setter<TEnumerable, TElement> addDelegate)
    : XmlEnumerableConverter<TEnumerable, TElement>(elementConverter, getEnumerable)
{
    private readonly Setter<TEnumerable, TElement> _addDelegate = addDelegate;

    public override TEnumerable? Read(XmlReader reader)
    {
        if (default(TEnumerable) is null && reader.TryReadNullElement())
        {
            return default;
        }

        TEnumerable result = createObject();

        if (reader.IsEmptyElement)
        {
            reader.ReadStartElement();
            return result;
        }

        reader.ReadStartElement();
        XmlConverter<TElement> elementConverter = _elementConverter;
        Setter<TEnumerable, TElement> addDelegate = _addDelegate;

        while (reader.NodeType != XmlNodeType.EndElement)
        {
            if (reader.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            TElement? element = elementConverter.Read(reader);
            addDelegate(ref result, element!);
        }

        reader.ReadEndElement();
        return result;
    }
}

internal sealed class XmlParameterizedEnumerableConverter<TEnumerable, TElement>(
    XmlConverter<TElement> elementConverter,
    Func<TEnumerable, IEnumerable<TElement>> getEnumerable,
    ParameterizedCollectionConstructor<TElement, TElement, TEnumerable> spanConstructor)
    : XmlEnumerableConverter<TEnumerable, TElement>(elementConverter, getEnumerable)
{
    public sealed override TEnumerable? Read(XmlReader reader)
    {
        if (default(TEnumerable) is null && reader.TryReadNullElement())
        {
            return default;
        }

        using PooledList<TElement> elements = new();

        if (reader.IsEmptyElement)
        {
            reader.ReadStartElement();
            return spanConstructor(elements.AsSpan());
        }

        XmlConverter<TElement> elementConverter = _elementConverter;

        reader.ReadStartElement();
        while (reader.NodeType != XmlNodeType.EndElement)
        {
            if (reader.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            TElement? element = elementConverter.Read(reader);
            elements.Add(element!);
        }

        reader.ReadEndElement();
        return spanConstructor(elements.AsSpan());
    }
}
