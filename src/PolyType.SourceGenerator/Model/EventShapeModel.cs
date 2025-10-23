using PolyType.Roslyn;

namespace PolyType.SourceGenerator.Model;

public sealed record EventShapeModel
{
    public required string Name { get; init; }
    public required string UnderlyingMemberName { get; init; }
    public required TypeId HandlerType { get; init; }
    public required TypeId DeclaringType { get; init; }
    public required bool IsAccessible { get; init; }
    public required bool CanUseUnsafeAccessors { get; init; }
    public required bool RequiresDisambiguation { get; init; }
    public required bool IsPublic { get; init; }
    public required bool IsStatic { get; init; }

    /// <summary>
    /// List of attribute data to be emitted for this event.
    /// </summary>
    public ImmutableEquatableArray<AttributeDataModel> Attributes { get; init; } = ImmutableEquatableArray<AttributeDataModel>.Empty;
}
