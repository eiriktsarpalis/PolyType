using System.Xml;

namespace PolyType.Examples.XmlSerializer;

/// <summary>
/// Defines a strongly typed XML to .NET converter.
/// </summary>
public abstract class XmlConverter<T> : IXmlConverter
{
    /// <summary>
    /// Writes a value of type <typeparamref name="T"/> to the provided <see cref="XmlWriter"/>.
    /// </summary>
    public abstract void Write(XmlWriter writer, T value);

    /// <summary>
    /// Reads a value of type <typeparamref name="T"/> from the provided <see cref="XmlReader"/>.
    /// </summary>
    public abstract T? Read(XmlReader reader);

    Type IXmlConverter.Type => typeof(T);
    void IXmlConverter.Write(XmlWriter writer, object value) => Write(writer, (T)value);
    object? IXmlConverter.Read(XmlReader reader) => Read(reader);
}

internal interface IXmlConverter
{
    Type Type { get; }
    void Write(XmlWriter writer, object value);
    object? Read(XmlReader reader);
}