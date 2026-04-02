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

    /// <summary>
    /// Writes just the mapping content (key-value pairs) without emitting MappingStart/MappingEnd.
    /// Used by union converters to merge the discriminator key into the inner mapping.
    /// Non-object types are wrapped under a "_value" key.
    /// </summary>
    internal virtual void WriteMappingContent(YamlWriter writer, T value)
    {
        writer.WriteKey("_value");
        Write(writer, value);
    }

    /// <summary>
    /// Reads mapping content (key-value pairs) without consuming MappingStart/MappingEnd.
    /// Used by union converters to read inner properties after the discriminator.
    /// Non-object types are expected under a "_value" key.
    /// </summary>
    internal virtual T? ReadMappingContent(YamlReader reader)
    {
        if (reader.TryReadMappingKey(out string key) && key == "_value")
        {
            return Read(reader);
        }

        throw new NotSupportedException($"Deserialization not supported for type {typeof(T)}.");
    }

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
