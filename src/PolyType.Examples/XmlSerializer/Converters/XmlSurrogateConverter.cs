using System.Xml;

namespace PolyType.Examples.XmlSerializer.Converters;

internal sealed class XmlSurrogateConverter<T, TSurrogate>(IMarshaller<T, TSurrogate> marshaller, XmlConverter<TSurrogate> surrogateConverter)
    : XmlConverter<T>
{
    public override void Write(XmlWriter writer, string localName, T? value) =>
        surrogateConverter.Write(writer, localName, marshaller.ToSurrogate(value));

    public override T? Read(XmlReader reader) =>
        marshaller.FromSurrogate(surrogateConverter.Read(reader));
}