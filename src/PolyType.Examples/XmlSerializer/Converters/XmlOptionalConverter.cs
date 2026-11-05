using PolyType.Abstractions;
using System.Xml;

namespace PolyType.Examples.XmlSerializer.Converters;

internal sealed class XmlOptionalConverter<TOptional, TElement>(
    XmlConverter<TElement> elementConverter,
    OptionDeconstructor<TOptional, TElement> deconstructor,
    Func<TOptional> createNone,
    Func<TElement, TOptional> createSome) : XmlConverter<TOptional>
{
    public override TOptional? Read(XmlReader reader)
    {
        // Additional attributes describe a present value; let its converter interpret them.
        if (reader.AttributeCount <= 1 && reader.TryReadNullElement())
        {
            return createNone();
        }

        return createSome(elementConverter.Read(reader)!);
    }

    public override void Write(XmlWriter writer, TOptional value)
    {
        if (deconstructor(value, out TElement? element))
        {
            elementConverter.Write(writer, element);
            return;
        }

        writer.WriteAttributeString("nil", "true");
    }
}
