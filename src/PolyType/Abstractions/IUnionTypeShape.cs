namespace PolyType.Abstractions;

/// <summary>
/// Provides a strongly typed shape model for a .NET type encoding a discriminated union.
/// </summary>
/// <remarks>
/// Typically reserved for classes or interfaces that specify derived types via the <see cref="DerivedTypeShapeAttribute"/>
/// but can also include F# discriminated unions.
/// </remarks>
public interface IUnionTypeShape : ITypeShape
{
    /// <summary>
    /// Gets the underlying type shape of the union base type.
    /// </summary>
    /// <remarks>
    /// Typically used as the fallback case for values not matching any of the union cases.
    /// </remarks>
    ITypeShape BaseType { get; }

    /// <summary>
    /// Gets the list of all registered union case shapes.
    /// </summary>
    IReadOnlyList<IUnionCaseShape> UnionCases { get; }
}

/// <summary>
/// Provides a strongly typed shape model for a .NET type encoding a discriminated union.
/// </summary>
/// <typeparam name="TUnion">The type of the union.</typeparam>
/// <remarks>
/// Typically reserved for classes or interfaces that specify derived types via the <see cref="DerivedTypeShapeAttribute"/>
/// but can also include F# discriminated unions.
/// </remarks>
public interface IUnionTypeShape<TUnion> : ITypeShape<TUnion>, IUnionTypeShape
{
    /// <summary>
    /// Gets the underlying type shape of the union base type.
    /// </summary>
    /// <remarks>
    /// Typically used as the fallback case for values not matching any of the union cases.
    /// </remarks>
    new ITypeShape<TUnion> BaseType { get; }

    /// <summary>
    /// Creates a delegate that computes the union case index for a given value.
    /// </summary>
    /// <returns>A delegate that computes the union case index for a given value.</returns>
    /// <remarks>
    /// The delegate returns an index pointing to the <see cref="IUnionTypeShape.UnionCases"/> list.
    /// It should be noted that the value of the index is distinct from the <see cref="IUnionCaseShape.Tag"/> property.
    /// </remarks>
    Getter<TUnion, int> GetGetUnionCaseIndex();
}