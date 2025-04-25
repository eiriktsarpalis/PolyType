using PolyType.Roslyn;

namespace PolyType.SourceGenerator.Model;

public sealed record ConstructorShapeModel
{
    public required TypeId DeclaringType { get; init; }
    public required bool IsPublic { get; init; }
    public required bool IsAccessible { get; init; }
    public required bool CanUseUnsafeAccessors { get; init; }
    public required ImmutableEquatableArray<ParameterShapeModel> Parameters { get; init; }
    public required ImmutableEquatableArray<ParameterShapeModel> RequiredMembers { get; init; }
    public required ImmutableEquatableArray<ParameterShapeModel> RequiredParametersAndMembers { get; init; }
    public required OptionalMemberFlagsType RequiredMemberFlagsType { get; init; }
    public required ImmutableEquatableArray<ParameterShapeModel> OptionalMembers { get; init; }
    public required OptionalMemberFlagsType OptionalMemberFlagsType { get; init; }
    public required string? StaticFactoryName { get; init; }
    public required bool StaticFactoryIsProperty { get; init; }
    public required bool ResultRequiresCast { get; init; }

    public int TotalArity => Parameters.Length + RequiredMembers.Length + OptionalMembers.Length;
    public bool IsStaticFactory => StaticFactoryName != null;
}

/// <summary>
/// Type used to store flags for whether optional members are set.
/// </summary>
public enum OptionalMemberFlagsType
{
    None,
    Byte,
    UShort,
    UInt32,
    ULong,
    BitArray,
}