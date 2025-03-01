using PolyType.Abstractions;
using System.Xml;

namespace PolyType.Examples.XmlSerializer.Converters;

internal sealed class XmlUnionConverter<TUnion>(
    Getter<TUnion, int> getUnionCaseIndex,
    XmlConverter<TUnion> baseConverter,
    KeyValuePair<string, XmlConverter<TUnion>>[] unionCaseConverters) : XmlConverter<TUnion>
{
    private const string TypeAttribute = "type";
    private readonly Dictionary<string, XmlConverter<TUnion>> _unionCaseConverters = unionCaseConverters.ToDictionary(p => p.Key, p => p.Value);

    public override TUnion? Read(XmlReader reader)
    {
        if (default(TUnion) is null && reader.TryReadNullElement())
        {
            return default;
        }

        if (reader.GetAttribute(TypeAttribute) is not string typeName)
        {
            return baseConverter.Read(reader);
        }

        if (!_unionCaseConverters.TryGetValue(typeName, out XmlConverter<TUnion>? derivedConverter))
        {
            throw new XmlException($"Unrecognized derived type identifier '{reader.Name}'.");
        }

        return derivedConverter.Read(reader);
    }

    public override void Write(XmlWriter writer, TUnion value)
    {
        int index = getUnionCaseIndex(ref value);
        if (index < 0)
        {
            baseConverter.Write(writer, value);
            return;
        }

        var entry = unionCaseConverters[index];
        writer.WriteAttributeString(TypeAttribute, entry.Key);
        entry.Value.Write(writer, value);
    }
}
