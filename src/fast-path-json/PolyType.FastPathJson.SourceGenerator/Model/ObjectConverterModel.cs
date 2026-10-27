using PolyType.Roslyn;

namespace PolyType.FastPathJson.SourceGenerator.Model;

internal sealed record ObjectConverterModel(
    TypeRef Type,
    ImmutableEquatableArray<WriteProperty> Properties,
    ImmutableEquatableArray<ReadSlot> Slots,
    string? Construction,
    ImmutableEquatableArray<MemberAssignment> Assignments,
    ImmutableEquatableArray<UnionCase> UnionCases) : ConverterModel(Type);
