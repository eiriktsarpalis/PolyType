namespace PolyType.FastPathJson.SourceGenerator.Model;

internal sealed record BuiltInCollectionConverterModel(
    TypeRef Type, TypeRef Element, TypeRef? Key = null) : ConverterModel(Type);
