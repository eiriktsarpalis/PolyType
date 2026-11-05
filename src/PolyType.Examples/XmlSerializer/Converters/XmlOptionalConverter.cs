using PolyType.Abstractions;
using System.Xml;

namespace PolyType.Examples.XmlSerializer.Converters;

internal sealed class XmlOptionalConverter<TOptional, TElement>(
    XmlConverter<TElement> elementConverter,
    bool elementIsUnion,
    OptionDeconstructor<TOptional, TElement> deconstructor,
    Func<TOptional> createNone,
    Func<TElement, TOptional> createSome) : XmlConverter<TOptional>
{
    public override TOptional? Read(XmlReader reader)
    {
        // For union elements, attributes besides nil identify a present case with a null payload.
        // Keep this check here so the selected payload converter can still read nil as null.
        if ((!elementIsUnion || reader.AttributeCount <= 1) && reader.TryReadNullElement())
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
