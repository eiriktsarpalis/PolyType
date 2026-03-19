using Microsoft.CodeAnalysis;
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
    public required ImmutableEquatableArray<ParameterShapeModel> OptionalMembers { get; init; }
    public required ArgumentStateType ArgumentStateType { get; init; }
    public required string? StaticFactoryName { get; init; }
    public required bool StaticFactoryIsProperty { get; init; }
    public required bool ResultRequiresCast { get; init; }
    public required bool IsFSharpUnitConstructor { get; init; }
    public required ImmutableEquatableArray<AttributeDataModel> Attributes { get; init; }

    public int TotalArity => Parameters.Count(p => p.RefKind is not RefKind.Out) + RequiredMembers.Length + OptionalMembers.Length;
    public bool IsStaticFactory => StaticFactoryName != null;
    public bool HasOutParameters => Parameters.Any(p => p.RefKind is RefKind.Out);

    /// <summary>Gets the parameters that are included in the argument state (excludes <c>out</c> parameters).</summary>
    public IEnumerable<ParameterShapeModel> ShapedParameters => Parameters.Where(p => p.RefKind is not RefKind.Out);

    public IEnumerable<ParameterShapeModel> GetAllParameters()
    {
        foreach (var param in ShapedParameters)
        {
            yield return param;
        }
        foreach (var member in RequiredMembers)
        {
            yield return member;
        }
        foreach (var member in OptionalMembers)
        {
            yield return member;
        }
    }
}

/// <summary>
/// Type used to store the state of arguments for a constructor.
/// </summary>
public enum ArgumentStateType
{
    EmptyArgumentState,
    SmallArgumentState,
    LargeArgumentState,
}