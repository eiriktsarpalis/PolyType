using System.Xml;

namespace PolyType.Examples.XmlSerializer.Converters;

/// <summary>
/// Hides the consumed union discriminator from the payload converter without buffering XML.
/// </summary>
internal sealed class XmlUnionPayloadReader : XmlReader
{
    private readonly XmlReader _reader;
    private readonly string _discriminator;
    private readonly int _discriminatorIndex;
    private bool _hideDiscriminator = true;

    public XmlUnionPayloadReader(XmlReader reader, string discriminator)
    {
        _reader = reader;
        _discriminator = discriminator;
        for (int i = 0; i < reader.AttributeCount; i++)
        {
            reader.MoveToAttribute(i);
            if (reader.Name == discriminator)
            {
                _discriminatorIndex = i;
                reader.MoveToElement();
                return;
            }
        }

        reader.MoveToElement();
        throw new XmlException($"Missing union discriminator '{discriminator}'.");
    }

    public override int AttributeCount => _reader.AttributeCount - (_hideDiscriminator ? 1 : 0);
    public override string BaseURI => _reader.BaseURI;
    public override int Depth => _reader.Depth;
    public override bool EOF => _reader.EOF;
    public override bool HasValue => _reader.HasValue;
    public override bool IsEmptyElement => _reader.IsEmptyElement;
    public override string LocalName => _reader.LocalName;
    public override string NamespaceURI => _reader.NamespaceURI;
    public override XmlNameTable NameTable => _reader.NameTable;
    public override XmlNodeType NodeType => _reader.NodeType;
    public override string Prefix => _reader.Prefix;
    public override ReadState ReadState => _reader.ReadState;
    public override string Value => _reader.Value;
    public override XmlReaderSettings? Settings => _reader.Settings;
    public override string XmlLang => _reader.XmlLang;
    public override XmlSpace XmlSpace => _reader.XmlSpace;

    public override string GetAttribute(int i) => _reader.GetAttribute(GetUnderlyingIndex(i));
    public override string? GetAttribute(string name) =>
        IsDiscriminator(name, null) ? null : _reader.GetAttribute(name);
    public override string? GetAttribute(string name, string? namespaceURI) =>
        IsDiscriminator(name, namespaceURI) ? null : _reader.GetAttribute(name, namespaceURI);
    public override string? LookupNamespace(string prefix) => _reader.LookupNamespace(prefix);

    public override void MoveToAttribute(int i) => _reader.MoveToAttribute(GetUnderlyingIndex(i));
    public override bool MoveToAttribute(string name) =>
        !IsDiscriminator(name, null) && _reader.MoveToAttribute(name);
    public override bool MoveToAttribute(string name, string? ns) =>
        !IsDiscriminator(name, ns) && _reader.MoveToAttribute(name, ns);
    public override bool MoveToElement() => _reader.MoveToElement();

    public override bool MoveToFirstAttribute()
    {
        if (AttributeCount == 0)
        {
            return false;
        }

        MoveToAttribute(0);
        return true;
    }

    public override bool MoveToNextAttribute()
    {
        bool wasAttribute = _reader.NodeType is XmlNodeType.Attribute;
        string localName = _reader.LocalName;
        string namespaceURI = _reader.NamespaceURI;
        if (!_reader.MoveToNextAttribute())
        {
            return false;
        }

        if (!IsDiscriminator(_reader.Name, _reader.NamespaceURI) || _reader.MoveToNextAttribute())
        {
            return true;
        }

        if (wasAttribute)
        {
            _reader.MoveToAttribute(localName, namespaceURI);
        }
        else
        {
            _reader.MoveToElement();
        }

        return false;
    }

    public override bool Read()
    {
        _hideDiscriminator = false;
        return _reader.Read();
    }

    public override bool ReadAttributeValue() => _reader.ReadAttributeValue();
    public override void ResolveEntity() => _reader.ResolveEntity();
    public override void Close() => _reader.Close();

    private bool IsDiscriminator(string name, string? namespaceURI) =>
        _hideDiscriminator && name == _discriminator && string.IsNullOrEmpty(namespaceURI);

    private int GetUnderlyingIndex(int index)
    {
        if ((uint)index >= (uint)AttributeCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return _hideDiscriminator && index >= _discriminatorIndex ? index + 1 : index;
    }
}
