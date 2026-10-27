namespace PolyType.FastPathJson.SourceGenerator.Model;

internal sealed record ReadSlot(string Name, TypeRef Type, string DefaultValue, bool Required, bool DisallowNull);
