using PolyType.Roslyn;

namespace PolyType.SourceGenerator.Model;

/// <summary>
/// Describes the open declaring type of a generic unsafe accessor and its closed call-site arguments.
/// </summary>
public sealed record GenericTypeModel
{

    /// <summary>Gets the fully qualified name of the generic type definition.</summary>
    public required string FullyQualifiedName { get; init; }

    /// <summary>Gets the escaped names of the type parameters.</summary>
    public required ImmutableEquatableArray<string> TypeParameters { get; init; }

    /// <summary>Gets the fully qualified names of the closed type arguments.</summary>
    public required ImmutableEquatableArray<string> TypeArguments { get; init; }

    /// <summary>Gets the constraint clauses for the type parameters.</summary>
    public required string ConstraintClauses { get; init; }
}
