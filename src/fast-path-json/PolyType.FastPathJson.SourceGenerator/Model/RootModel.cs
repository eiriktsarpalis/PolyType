using PolyType.Roslyn;

namespace PolyType.FastPathJson.SourceGenerator.Model;

internal sealed record RootModel(
    string SourceIdentifier,
    string? Namespace,
    ImmutableEquatableArray<string> Declarations,
    TypeRef Type);
