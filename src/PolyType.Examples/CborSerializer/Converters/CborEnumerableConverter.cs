using System.Formats.Cbor;
using PolyType.Abstractions;
using PolyType.Examples.Utilities;

namespace PolyType.Examples.CborSerializer.Converters;

internal class CborEnumerableConverter<TEnumerable, TElement>(
    CborConverter<TElement> elementConverter,
    Func<TEnumerable, IEnumerable<TElement>> getEnumerable) : CborConverter<TEnumerable>
{
    private protected readonly CborConverter<TElement> _elementConverter = elementConverter;

    public override TEnumerable? Read(CborReader reader)
    {
        if (default(TEnumerable) is null && reader.PeekState() == CborReaderState.Null)
        {
            reader.ReadNull();
            return default;
        }

        throw new NotSupportedException($"Deserialization not supported for type {typeof(TEnumerable)}.");
    }

    public sealed override void Write(CborWriter writer, TEnumerable? value)
    {
        if (value is null)
        {
            writer.WriteNull();
            return;
        }

        IEnumerable<TElement> enumerable = getEnumerable(value);
        int? definiteLength = enumerable.TryGetNonEnumeratedCount(out int count) ? count : null;
        CborConverter<TElement> elementConverter = _elementConverter;

        writer.WriteStartArray(definiteLength);
        foreach (TElement element in enumerable)
        {
            elementConverter.Write(writer, element);
        }
        writer.WriteEndArray();
    }
}

internal sealed class CborMutableEnumerableConverter<TEnumerable, TElement>(
    CborConverter<TElement> elementConverter,
    Func<TEnumerable, IEnumerable<TElement>> getEnumerable,
    MutableCollectionConstructor<TElement, TEnumerable> createObject,
    EnumerableAppender<TEnumerable, TElement> appender) : CborEnumerableConverter<TEnumerable, TElement>(elementConverter, getEnumerable)
{
    private readonly EnumerableAppender<TEnumerable, TElement> _appender = appender;

    public override TEnumerable? Read(CborReader reader)
    {
        if (default(TEnumerable) is null && reader.PeekState() == CborReaderState.Null)
        {
            reader.ReadNull();
            return default;
        }

        int? definiteLength = reader.ReadStartArray();
        TEnumerable result = createObject(new() { Capacity = definiteLength });

        CborConverter<TElement> elementConverter = _elementConverter;
        EnumerableAppender<TEnumerable, TElement> appender = _appender;

        while (reader.PeekState() != CborReaderState.EndArray)
        {
            TElement? element = elementConverter.Read(reader);
            appender(ref result, element!);
        }

        reader.ReadEndArray();
        return result;
    }
}

internal sealed class CborParameterizedEnumerableConverter<TEnumerable, TElement>(
    CborConverter<TElement> elementConverter,
    Func<TEnumerable, IEnumerable<TElement>> getEnumerable,
    ParameterizedCollectionConstructor<TElement, TElement, TEnumerable> constructor)
    : CborEnumerableConverter<TEnumerable, TElement>(elementConverter, getEnumerable)
{
    public sealed override TEnumerable? Read(CborReader reader)
    {
        if (default(TEnumerable) is null && reader.PeekState() == CborReaderState.Null)
        {
            reader.ReadNull();
            return default;
        }

        int? definiteLength = reader.ReadStartArray();
        using PooledList<TElement> buffer = new(definiteLength ?? 4);
        CborConverter<TElement> elementConverter = _elementConverter;

        while (reader.PeekState() != CborReaderState.EndArray)
        {
            TElement? element = elementConverter.Read(reader);
            buffer.Add(element!);
        }

        reader.ReadEndArray();
        return constructor(buffer.AsSpan());
    }
}