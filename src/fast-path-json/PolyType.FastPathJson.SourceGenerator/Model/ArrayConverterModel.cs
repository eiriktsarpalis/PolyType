namespace PolyType.FastPathJson.SourceGenerator.Model;

internal sealed record ArrayConverterModel(TypeRef Type, TypeRef Element, int Rank, string Construction) : ConverterModel(Type);
