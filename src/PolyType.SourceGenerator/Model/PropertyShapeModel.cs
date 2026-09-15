using PolyType.Roslyn;

namespace PolyType.SourceGenerator.Model;

public sealed record PropertyShapeModel
{
    public required string Name { get; init; }
    public required string UnderlyingMemberName { get; init; }
    public required TypeId DeclaringType { get; init; }
    public required TypeId PropertyType { get; init; }
    
    public required bool IsField { get; init; }
    public required bool IsInitOnly { get; init; }

    public required bool EmitGetter { get; init; }
    public required bool EmitSetter { get; init; }

    public required bool IsGetterAccessible { get; init; }
    public required bool IsSetterAccessible { get; init; }

    public required bool IsGetterPublic { get; init; }
    public required bool IsSetterPublic { get; init; }

    public required bool IsGetterNonNullable { get; init; }
    public required bool IsSetterNonNullable { get; init; }

    public required bool IsRequiredBySyntax { get; init; }
    public required bool? IsRequiredByPolicy { get; init; }
    
    /// <summary>
    /// Whether the property type or type parameters of the
    /// property type contain nullability annotations.
    /// </summary>
    public required bool PropertyTypeContainsNullabilityAnnotations { get; init; }

    /// <summary>
    /// Determines if the property can use unsafe accessors in the generated code.
    /// </summary>
    public required bool CanUseUnsafeAccessors { get; init; }

    /// <summary>Gets the position of the declaring type in the shaped type's hierarchy.</summary>
    public int DeclaringTypeIndex { get; init; }

    /// <summary>Gets the open declaring type when a generic unsafe accessor is needed.</summary>
    public GenericTypeModel? GenericDeclaringType { get; init; }

    /// <summary>Gets the property type in the generic type definition, when needed.</summary>
    public string? OpenPropertyTypeName { get; init; }

    public required int Order { get; init; }

    public required bool RequiresDisambiguation { get; init; }
    public required ImmutableEquatableArray<AttributeDataModel> Attributes { get; init; }
}
