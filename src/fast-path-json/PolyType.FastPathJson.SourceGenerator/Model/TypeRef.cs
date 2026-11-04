namespace PolyType.FastPathJson.SourceGenerator.Model;

internal sealed record TypeRef(
    string Name, string Converter, bool IsValueType, bool SupportsPropertyNames = false, string? Witness = null, string? ReadConverter = null)
{
    public string NullableName => IsValueType ? Name : Name + "?";

    public string WitnessType => Witness ?? Converter;

    public string ReadWitnessType => Witness ?? ReadConverter ?? Converter;
}
