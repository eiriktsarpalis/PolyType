namespace PolyType.Abstractions;

/// <summary>
/// Provides a shape model for a union case.
/// </summary>
[InternalImplementationsOnly]
public interface IUnionCaseShape
{
    /// <summary>
    /// Gets the unique string identifier for the current union case.
    /// </summary>
    /// <remarks>
    /// The value is usually the name of the derived type but can be
    /// overridden via the <see cref="DerivedTypeShapeAttribute.Name"/> property.
    /// </remarks>
    string Name { get; }

    /// <summary>
    /// Gets the unique integer identifier for the current union case.
    /// </summary>
    /// <remarks>
    /// The value typically corresponds to the order of <see cref="DerivedTypeShapeAttribute"/> declarations
    /// but can be overridden via the <see cref="DerivedTypeShapeAttribute.Tag"/> property.
    /// </remarks>
    int Tag { get; }

    /// <summary>
    /// Gets a value indicating whether <see cref="Tag"/> has been explicitly specified or inferred in a less stable way.
    /// </summary>
    bool IsTagSpecified { get; }

    /// <summary>
    /// Gets a value indicating whether this case admits a null payload.
    /// </summary>
    /// <remarks>
    /// For C# unions, this reflects the nullable contract of the case's creation parameter.
    /// Always returns <see langword="false"/> for F# unions and type hierarchies.
    /// A case's use of a null CLR representation does not make its payload nullable.
    /// </remarks>
    bool IsNullable { get; }

    /// <summary>
    /// Gets the unique index corresponding to the current union case.
    /// </summary>
    /// <remarks>
    /// Corresponds to the index of this instance in the parent <see cref="IUnionTypeShape.UnionCases"/> list.
    /// While similar to <see cref="Tag"/>, the value of the index is distinct and is not part of the data contract.
    /// </remarks>
    int Index { get; }

    /// <summary>
    /// Gets the underlying type shape of the union case.
    /// </summary>
    /// <remarks>
    /// F# case bodies and explicitly registered hierarchy base cases are contextual views.
    /// C# payload shapes and proper-derived hierarchy case shapes are non-contextual.
    /// </remarks>
    ITypeShape UnionCaseType { get; }

    /// <summary>
    /// Accepts an <see cref="TypeShapeVisitor"/> for strongly-typed traversal.
    /// </summary>
    /// <param name="visitor">The visitor to accept.</param>
    /// <param name="state">The state parameter to pass to the underlying visitor.</param>
    /// <returns>The <see cref="object"/> result returned by the visitor.</returns>
    object? Accept(TypeShapeVisitor visitor, object? state = null);
}

/// <summary>
/// Provides a strongly typed shape model for a union case.
/// </summary>
/// <typeparam name="TUnionCase">The type of the union case.</typeparam>
/// <typeparam name="TUnion">The type of the underlying union.</typeparam>
[InternalImplementationsOnly]
public interface IUnionCaseShape<TUnionCase, TUnion> : IUnionCaseShape
{
    /// <summary>
    /// Gets the underlying type shape of the union case.
    /// </summary>
    /// <remarks>
    /// This can be a contextual view rather than the provider's ordinary shape for
    /// <typeparamref name="TUnionCase"/>.
    /// </remarks>
    new ITypeShape<TUnionCase> UnionCaseType { get; }

    /// <summary>
    /// Gets a bidirectional mapper between <typeparamref name="TUnionCase"/> and <typeparamref name="TUnion"/>.
    /// </summary>
    /// <remarks>
    /// Converting from <typeparamref name="TUnionCase"/> to <typeparamref name="TUnion"/> is a reversible operation,
    /// however mapping from <typeparamref name="TUnion"/> to <typeparamref name="TUnionCase"/> can fail if the
    /// value is not a member of the current union case.
    /// </remarks>
    IMarshaler<TUnionCase, TUnion> Marshaler { get; }
}