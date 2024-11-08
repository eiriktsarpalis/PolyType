﻿using System.Xml;
using PolyType.Examples.Utilities;

namespace PolyType.Examples.XmlSerializer.Converters;

internal sealed class DelayedXmlConverter<T>(ResultBox<XmlConverter<T>> self) : XmlConverter<T>
{
    public override T? Read(XmlReader reader) =>
        self.Result.Read(reader);

    public override void Write(XmlWriter writer, string localName, T? value) =>
        self.Result.Write(writer, localName, value);
}
