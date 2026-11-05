using System.Runtime.CompilerServices;
using System.Xml;

namespace PolyType.Examples.XmlSerializer.Converters;

internal static class XmlHelpers
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void EnsureRead(this XmlReader reader)
    {
        if (!reader.Read())
        {
            Throw();
            static void Throw() => throw new InvalidOperationException("Unexpected end of XML stream.");
        }
    }

    public static void WriteAsElement<T>(this XmlConverter<T> converter, XmlWriter writer, string localName, T? value)
    {
        writer.WriteStartElement(localName);
        
        if (value is null)
        {
            writer.WriteAttributeString("nil", "true");
        }
        else
        {
            converter.Write(writer, value);
        }

        writer.WriteEndElement();
    }

    public static bool IsNullElement(this XmlReader reader)
    {
        string? attribute = reader.GetAttribute("nil");
        return attribute != null && XmlConvert.ToBoolean(attribute);
    }

    public static bool TryReadNullElement(this XmlReader reader)
    {
        if (reader.IsNullElement())
        {
            reader.Read();
            return true;
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static XmlNodeType ReadNextNode(this XmlReader reader)
    {
        reader.Read();
        return reader.NodeType;
    }
}
