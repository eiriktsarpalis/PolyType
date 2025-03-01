namespace PolyType.SourceGenerator.Model;

public sealed record OptionalShapeModel : TypeShapeModel
{
    public required TypeId ElementType { get; init; }
    public required OptionalKind Kind { get; init; }
}

public enum OptionalKind
{
    NullableOfT,
    FSharpOption,
    FSharpValueOption,
}
