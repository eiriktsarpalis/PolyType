using PolyType.Roslyn;

namespace PolyType.SourceGenerator.Model;

public sealed record EnumShapeModel : TypeShapeModel
{
    public required TypeId UnderlyingType { get; init; }

    public required ImmutableEquatableDictionary<string, string> Members { get; init; }
}
