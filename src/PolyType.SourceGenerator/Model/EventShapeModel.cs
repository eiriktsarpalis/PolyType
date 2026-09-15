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

    /// <summary>Gets the position of the declaring type in the shaped type's hierarchy.</summary>
    public int DeclaringTypeIndex { get; init; }

    /// <summary>Gets the open declaring type when a generic unsafe accessor is needed.</summary>
    public GenericTypeModel? GenericDeclaringType { get; init; }

    /// <summary>Gets the handler type in the generic type definition, when needed.</summary>
    public string? OpenHandlerTypeName { get; init; }

    public required bool RequiresDisambiguation { get; init; }
    public required bool IsPublic { get; init; }
    public required bool IsStatic { get; init; }
    public required ImmutableEquatableArray<AttributeDataModel> Attributes { get; init; }
}
