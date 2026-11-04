namespace PolyType.FastPathJson.SourceGenerator.Model;

internal sealed record EnumerableConverterModel(
    TypeRef Type,
    TypeRef Element,
    string Construction,
    string? Append,
    bool RequiresInterfaceEnumeration) : ConverterModel(Type);
