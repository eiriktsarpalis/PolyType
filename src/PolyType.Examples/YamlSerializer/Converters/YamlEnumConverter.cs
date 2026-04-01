namespace PolyType.Examples.YamlSerializer.Converters;

internal sealed class YamlEnumConverter<TEnum> : YamlConverter<TEnum>
    where TEnum : struct, Enum
{
    public override TEnum Read(YamlReader reader)
#if NET
        => Enum.Parse<TEnum>(reader.ReadScalar()!);
#else
        => (TEnum)Enum.Parse(typeof(TEnum), reader.ReadScalar()!);
#endif

    public override void Write(YamlWriter writer, TEnum value)
        => writer.WriteString(value.ToString());
}
