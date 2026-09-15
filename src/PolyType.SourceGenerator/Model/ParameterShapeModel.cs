using Microsoft.CodeAnalysis;
using PolyType.Roslyn;

namespace PolyType.SourceGenerator.Model;

public sealed record ParameterShapeModel
{
    public required string Name { get; init; }
    public required string UnderlyingMemberName { get; init; }
    public required TypeId ParameterType { get; init; }
    public required TypeId DeclaringType { get; init; }
    public required ParameterKind Kind { get; init; }
    public required RefKind RefKind { get; init; }
    public required int Position { get; init; }
    public required bool IsRequired { get; init; }
    public required bool IsInitOnlyProperty { get; init; }
    public required bool IsNonNullable { get; init; }
    public required NullableAnnotation NullableAnnotation { get; init; }
    public required bool IsAccessible { get; init; }
    public required bool CanUseUnsafeAccessors { get; init; }

    /// <summary>Gets the position of a member initializer's declaring type in the shaped type's hierarchy.</summary>
    public int DeclaringTypeIndex { get; init; }

    /// <summary>Gets the open declaring type for a member initializer's generic unsafe accessor.</summary>
    public GenericTypeModel? GenericDeclaringType { get; init; }

    /// <summary>Gets the parameter type in the generic type definition, when needed.</summary>
    public string? OpenParameterTypeName { get; init; }

    public required bool ParameterTypeContainsNullabilityAnnotations { get; init; }
    public required bool IsPublic { get; init; }
    public required bool IsField { get; init; }
    public required bool HasDefaultValue { get; init; }
    public required string? DefaultValueExpr { get; init; }
    public required ImmutableEquatableArray<AttributeDataModel> Attributes { get; init; }
}

public enum ParameterKind
{
    MethodParameter,
    RequiredMember,
    OptionalMember
}