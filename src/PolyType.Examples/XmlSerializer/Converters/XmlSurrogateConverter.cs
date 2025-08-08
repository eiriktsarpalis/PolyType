using System.Xml;

namespace PolyType.Examples.XmlSerializer.Converters;

internal sealed class XmlSurrogateConverter<T, TSurrogate>(IMarshaler<T, TSurrogate> marshaler, XmlConverter<TSurrogate> surrogateConverter)
    : XmlConverter<T>
{
    public override void Write(XmlWriter writer, T value) => surrogateConverter.Write(writer, marshaler.Marshal(value)!);
    public override T? Read(XmlReader reader) => marshaler.Unmarshal(surrogateConverter.Read(reader));
}