namespace PolyType.Abstractions;

/// <summary>
/// Provides a strongly typed shape model for a .NET enum.
/// </summary>
[InternalImplementationsOnly]
public interface IEnumTypeShape : ITypeShape
{
    /// <summary>
    /// Gets the shape of the underlying type used to represent the enum.
    /// </summary>
    ITypeShape UnderlyingType { get; }
}

/// <summary>
/// Provides a strongly typed shape model for a .NET enum.
/// </summary>
/// <typeparam name="TEnum">The type of .NET enum.</typeparam>
/// <typeparam name="TUnderlying">The underlying type used to represent the enum.</typeparam>
[InternalImplementationsOnly]
public interface IEnumTypeShape<TEnum, TUnderlying> : ITypeShape<TEnum>, IEnumTypeShape
    where TEnum : struct, Enum
    where TUnderlying : unmanaged
{
    /// <summary>
    /// Gets the shape of the underlying type used to represent the enum.
    /// </summary>
    new ITypeShape<TUnderlying> UnderlyingType { get; }

    /// <summary>
    /// Gets the names and values for each enum member.
    /// </summary>
    /// <remarks>
    /// The dictionary uses <see cref="StringComparer.Ordinal" /> as its key comparer.
    /// </remarks>
    /// <devremarks>
    /// We use <typeparamref name="TUnderlying"/> instead of <typeparamref name="TEnum"/>
    /// to avoid needlessly closing generic types around unique value types (which swell the size of trimmed apps)
    /// when the caller can easily cast between enum and underlying types.
    /// </devremarks>
    IReadOnlyDictionary<string, TUnderlying> Members { get; }
}
