namespace PolyType.Examples.YamlSerializer.Converters;

internal sealed class YamlEnumConverter<TEnum> : YamlConverter<TEnum>
    where TEnum : struct, Enum
{
    public override TEnum Read(YamlReader reader)
        => Enum.Parse<TEnum>(reader.ReadScalar()!);

    public override void Write(YamlWriter writer, TEnum value)
        => writer.WriteString(value.ToString());
}
