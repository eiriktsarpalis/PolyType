using PolyType.Abstractions;
using System.Xml;

namespace PolyType.Examples.XmlSerializer.Converters;

internal sealed class XmlOptionalConverter<TOptional, TElement>(
    XmlConverter<TElement> elementConverter,
    OptionDeconstructor<TOptional, TElement> deconstructor,
    Func<TOptional> createNone,
    Func<TElement, TOptional> createSome) : XmlConverter<TOptional>
{
    public override TOptional? Read(XmlReader reader) => reader.TryReadNullElement() ? createNone() : createSome(elementConverter.Read(reader)!);
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
