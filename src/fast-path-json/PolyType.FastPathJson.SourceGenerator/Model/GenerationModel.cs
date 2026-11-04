using PolyType.Roslyn;

namespace PolyType.FastPathJson.SourceGenerator.Model;

internal sealed record GenerationModel(
    ImmutableEquatableArray<ConverterModel> Converters,
    ImmutableEquatableArray<RootModel> Roots);
