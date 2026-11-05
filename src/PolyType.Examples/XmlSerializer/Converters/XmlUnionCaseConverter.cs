using System.Xml;

namespace PolyType.Examples.XmlSerializer.Converters;

internal sealed class XmlUnionCaseConverter<TUnionCase, TUnion>(XmlConverter<TUnionCase> underlying, IMarshaler<TUnionCase, TUnion> marshaler) : XmlConverter<TUnion>
{
    public override TUnion? Read(XmlReader reader)
    {
        // The union discriminator has already selected this case; nil now describes its payload.
        if (default(TUnionCase) is null && reader.IsNullElement())
        {
            reader.Read();
            return marshaler.Marshal(default);
        }

        return marshaler.Marshal(underlying.Read(reader));
    }

    public override void Write(XmlWriter writer, TUnion value)
    {
        TUnionCase? payload = marshaler.Unmarshal(value);
        if (payload is null)
        {
            writer.WriteAttributeString("nil", "true");
        }
        else
        {
            underlying.Write(writer, payload);
        }
    }
}
