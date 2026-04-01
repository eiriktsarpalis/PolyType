using PolyType.Abstractions;
using PolyType.Examples.Utilities;

namespace PolyType.Examples.YamlSerializer.Converters;

internal class YamlEnumerableConverter<TEnumerable, TElement>(
    YamlConverter<TElement> elementConverter,
    Func<TEnumerable, IEnumerable<TElement>> getEnumerable)
    : YamlConverter<TEnumerable>
{
    private protected readonly YamlConverter<TElement> _elementConverter = elementConverter;
    private readonly Func<TEnumerable, IEnumerable<TElement>> _getEnumerable = getEnumerable;

    public override TEnumerable? Read(YamlReader reader)
    {
        if (default(TEnumerable) is null && reader.TryReadNull())
        {
            return default;
        }

        throw new NotSupportedException($"Deserialization not supported for type {typeof(TEnumerable)}.");
    }

    public override void Write(YamlWriter writer, TEnumerable value)
    {
        bool hasElements = false;
        foreach (TElement element in _getEnumerable(value))
        {
            if (!hasElements)
            {
                writer.BeginSequence();
                hasElements = true;
            }

            writer.WriteSequenceEntry();
            if (element is null)
            {
                writer.WriteNull();
            }
            else
            {
                _elementConverter.Write(writer, element);
            }
        }

        if (hasElements)
        {
            writer.EndSequence();
        }
        else
        {
            writer.WriteRawScalar("[]");
        }
    }
}

internal sealed class YamlMutableEnumerableConverter<TEnumerable, TElement>(
    YamlConverter<TElement> elementConverter,
    Func<TEnumerable, IEnumerable<TElement>> getEnumerable,
    MutableCollectionConstructor<TElement, TEnumerable> createObject,
    EnumerableAppender<TEnumerable, TElement> appender)
    : YamlEnumerableConverter<TEnumerable, TElement>(elementConverter, getEnumerable)
{
    private readonly EnumerableAppender<TEnumerable, TElement> _appender = appender;

    public override TEnumerable? Read(YamlReader reader)
    {
        if (default(TEnumerable) is null && reader.TryReadNull())
        {
            return default;
        }

        if (reader.TryReadEmptyCollection())
        {
            return createObject();
        }

        TEnumerable result = createObject();
        int expectedIndent = reader.CurrentIndent;
        EnumerableAppender<TEnumerable, TElement> appender = _appender;

        while (reader.TryReadSequenceEntry(expectedIndent, out string? inlineValue))
        {
            TElement? element;
            if (inlineValue is not null)
            {
                var inlineReader = new YamlReader(inlineValue);
                element = _elementConverter.Read(inlineReader);
            }
            else
            {
                element = _elementConverter.Read(reader);
            }

            appender(ref result, element!);
        }

        return result;
    }
}

internal sealed class YamlParameterizedEnumerableConverter<TEnumerable, TElement>(
    YamlConverter<TElement> elementConverter,
    Func<TEnumerable, IEnumerable<TElement>> getEnumerable,
    ParameterizedCollectionConstructor<TElement, TElement, TEnumerable> spanConstructor)
    : YamlEnumerableConverter<TEnumerable, TElement>(elementConverter, getEnumerable)
{
    public sealed override TEnumerable? Read(YamlReader reader)
    {
        if (default(TEnumerable) is null && reader.TryReadNull())
        {
            return default;
        }

        if (reader.TryReadEmptyCollection())
        {
            return spanConstructor([]);
        }

        using PooledList<TElement> elements = new();
        int expectedIndent = reader.CurrentIndent;

        while (reader.TryReadSequenceEntry(expectedIndent, out string? inlineValue))
        {
            TElement? element;
            if (inlineValue is not null)
            {
                var inlineReader = new YamlReader(inlineValue);
                element = _elementConverter.Read(inlineReader);
            }
            else
            {
                element = _elementConverter.Read(reader);
            }

            elements.Add(element!);
        }

        return spanConstructor(elements.AsSpan());
    }
}
