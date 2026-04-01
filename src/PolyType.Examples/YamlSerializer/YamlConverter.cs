namespace PolyType.Examples.YamlSerializer;

/// <summary>
/// Defines a strongly typed YAML to .NET converter.
/// </summary>
public abstract class YamlConverter<T> : IYamlConverter
{
    /// <summary>
    /// Writes a value of type <typeparamref name="T"/> to the provided <see cref="YamlWriter"/>.
    /// </summary>
    public abstract void Write(YamlWriter writer, T value);

    /// <summary>
    /// Reads a value of type <typeparamref name="T"/> from the provided <see cref="YamlReader"/>.
    /// </summary>
    public abstract T? Read(YamlReader reader);

    Type IYamlConverter.Type => typeof(T);
    void IYamlConverter.Write(YamlWriter writer, object value) => Write(writer, (T)value);
    object? IYamlConverter.Read(YamlReader reader) => Read(reader);
}

internal interface IYamlConverter
{
    Type Type { get; }
    void Write(YamlWriter writer, object value);
    object? Read(YamlReader reader);
}
