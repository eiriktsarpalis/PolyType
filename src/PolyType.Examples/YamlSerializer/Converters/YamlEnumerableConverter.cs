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
        writer.BeginSequence();
        foreach (TElement element in _getEnumerable(value))
        {
            if (element is null)
            {
                writer.WriteNull();
            }
            else
            {
                _elementConverter.Write(writer, element);
            }
        }

        writer.EndSequence();
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

        reader.ReadSequenceStart();
        TEnumerable result = createObject();
        EnumerableAppender<TEnumerable, TElement> appender = _appender;

        while (!reader.IsSequenceEnd)
        {
            TElement? element = _elementConverter.Read(reader);
            appender(ref result, element!);
        }

        reader.ReadSequenceEnd();

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

        reader.ReadSequenceStart();
        using PooledList<TElement> elements = new();

        while (!reader.IsSequenceEnd)
        {
            TElement? element = _elementConverter.Read(reader);
            elements.Add(element!);
        }

        reader.ReadSequenceEnd();

        return spanConstructor(elements.AsSpan());
    }
}
