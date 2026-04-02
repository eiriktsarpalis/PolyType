namespace PolyType.Examples.YamlSerializer.Converters;

internal sealed class YamlSurrogateConverter<T, TSurrogate>(IMarshaler<T, TSurrogate> marshaler, YamlConverter<TSurrogate> surrogateConverter)
    : YamlConverter<T>
{
    public override void Write(YamlWriter writer, T value) => surrogateConverter.Write(writer, marshaler.Marshal(value)!);
    public override T? Read(YamlReader reader) => marshaler.Unmarshal(surrogateConverter.Read(reader));
    internal override void WriteMappingContent(YamlWriter writer, T value) => surrogateConverter.WriteMappingContent(writer, marshaler.Marshal(value)!);
    internal override T? ReadMappingContent(YamlReader reader) => marshaler.Unmarshal(surrogateConverter.ReadMappingContent(reader));
}
