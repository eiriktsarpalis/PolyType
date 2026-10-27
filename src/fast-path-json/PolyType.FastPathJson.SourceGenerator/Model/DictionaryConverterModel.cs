namespace PolyType.FastPathJson.SourceGenerator.Model;

internal sealed record DictionaryConverterModel(
    TypeRef Type,
    TypeRef Key,
    TypeRef Value,
    string Construction,
    string? Insert,
    bool RequiresInterfaceEnumeration) : ConverterModel(Type);
