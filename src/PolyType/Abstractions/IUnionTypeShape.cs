namespace PolyType.Abstractions;

/// <summary>
/// Provides a discriminated union shape model for a .NET type.
/// </summary>
/// <remarks>
/// Supports type hierarchies configured using <see cref="DerivedTypeShapeAttribute"/>,
/// F# discriminated unions, and types implementing the C# union member pattern.
/// </remarks>
[InternalImplementationsOnly]
public interface IUnionTypeShape : ITypeShape
{
    /// <summary>
    /// Gets the union representation described by this shape.
    /// </summary>
    /// <remarks>
    /// Returns <see cref="UnionTypeShapeKind.Unknown"/> when the representation is not specified,
    /// including shapes produced by older source generators.
    /// </remarks>
    UnionTypeShapeKind UnionKind { get; }

    /// <summary>
    /// Gets the underlying type shape of the union base type.
    /// </summary>
    /// <remarks>
    /// Type hierarchies use this shape as a fallback for values not matching a registered case.
    /// F# and C# unions expose an empty object shape without a constructor.
    /// </remarks>
    ITypeShape BaseType { get; }

    /// <summary>
    /// Gets the list of all registered union case shapes.
    /// </summary>
    IReadOnlyList<IUnionCaseShape> UnionCases { get; }
}

/// <summary>
/// Provides a strongly typed discriminated union shape model for a .NET type.
/// </summary>
/// <typeparam name="TUnion">The type of the union.</typeparam>
/// <remarks>
/// Supports type hierarchies configured using <see cref="DerivedTypeShapeAttribute"/>,
/// F# discriminated unions, and types implementing the C# union member pattern.
/// </remarks>
[InternalImplementationsOnly]
public interface IUnionTypeShape<TUnion> : ITypeShape<TUnion>, IUnionTypeShape
{
    /// <summary>
    /// Gets the underlying type shape of the union base type.
    /// </summary>
    /// <remarks>
    /// Type hierarchies use this shape as a fallback for values not matching a registered case.
    /// F# and C# unions expose an empty object shape without a constructor.
    /// </remarks>
    new ITypeShape<TUnion> BaseType { get; }

    /// <summary>
    /// Creates a delegate that computes the union case index for a given value.
    /// </summary>
    /// <returns>A delegate that computes the union case index for a given value.</returns>
    /// <remarks>
    /// The delegate returns an index pointing to the <see cref="IUnionTypeShape.UnionCases"/> list.
    /// It should be noted that the value of the index is distinct from the <see cref="IUnionCaseShape.Tag"/> property.
    /// An index of -1 indicates that no union case was selected.
    /// C# unions select the first nullable case for null values.
    /// For a C# union payload that matches no case, including null when no case admits it,
    /// the delegate throws <see cref="InvalidOperationException"/> as for a non-exhaustive switch instead of returning -1.
    /// </remarks>
    Getter<TUnion, int> GetGetUnionCaseIndex();
}