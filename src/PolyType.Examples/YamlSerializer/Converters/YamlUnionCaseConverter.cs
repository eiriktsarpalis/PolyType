namespace PolyType.Examples.YamlSerializer.Converters;

internal sealed class YamlUnionCaseConverter<TUnionCase, TUnion>(YamlConverter<TUnionCase> underlying, IMarshaler<TUnionCase, TUnion> marshaler) : YamlConverter<TUnion>
{
    public override TUnion? Read(YamlReader reader) => marshaler.Marshal(underlying.Read(reader));
    public override void Write(YamlWriter writer, TUnion value) => underlying.Write(writer, marshaler.Unmarshal(value)!);
}
