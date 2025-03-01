using System.Xml;

namespace PolyType.Examples.XmlSerializer.Converters;

internal sealed class XmlUnionCaseConverter<TUnionCase, TUnion>(XmlConverter<TUnionCase> underlying) : XmlConverter<TUnion>
    where TUnionCase : TUnion
{
    public override TUnion? Read(XmlReader reader) => underlying.Read(reader);
    public override void Write(XmlWriter writer, TUnion value) => underlying.Write(writer, (TUnionCase)value!);
}
