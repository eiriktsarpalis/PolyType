namespace PolyType.Abstractions;

/// <summary>
/// Identifies the union representation described by an <see cref="IUnionTypeShape"/>.
/// </summary>
public enum UnionTypeShapeKind
{
    /// <summary>
    /// The union representation is not specified, including shapes produced by older source generators.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// A class or interface hierarchy with registered derived types.
    /// </summary>
    TypeHierarchy = 1,

    /// <summary>
    /// A type implementing the C# union member pattern.
    /// </summary>
    CSharpUnion = 2,

    /// <summary>
    /// An F# discriminated union.
    /// </summary>
    FSharpUnion = 3,
}
