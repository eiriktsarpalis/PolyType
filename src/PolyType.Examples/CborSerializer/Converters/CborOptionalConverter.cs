using PolyType.Abstractions;
using System.Diagnostics;
using System.Formats.Cbor;

namespace PolyType.Examples.CborSerializer.Converters;

internal sealed class CborOptionalConverter<TOptional, TElement>(
    CborConverter<TElement> elementConverter,
    OptionDeconstructor<TOptional, TElement> deconstructor,
    Func<TOptional> createNone,
    Func<TElement, TOptional> createSome) : CborConverter<TOptional>
{
    public override TOptional Read(CborReader reader)
    {
        if (reader.PeekState() is CborReaderState.Null)
        {
            reader.ReadNull();
            return createNone();
        }

        TElement element = elementConverter.Read(reader)!;
        return createSome(element);
    }

    public override void Write(CborWriter writer, TOptional? value)
    {
        if (!deconstructor(value, out TElement? element))
        {
            writer.WriteNull();
            return;
        }

        elementConverter.Write(writer, element);
    }
}
