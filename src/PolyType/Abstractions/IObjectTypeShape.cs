namespace PolyType.Abstractions;

/// <summary>
/// Provides a strongly typed shape model for a .NET object.
/// </summary>
[InternalImplementationsOnly]
public interface IObjectTypeShape : ITypeShape
{
    /// <summary>
    /// Gets a value indicating whether the current shape represents a C# record type.
    /// </summary>
    bool IsRecordType { get; }

    /// <summary>
    /// Gets a value indicating whether the current shape represents a tuple type, either <see cref="System.Tuple"/> or <see cref="System.ValueTuple"/>.
    /// </summary>
    bool IsTupleType { get; }

    /// <summary>
    /// Gets all available property/field shapes for the given type.
    /// </summary>
    /// <returns>An enumeration of all available property/field shapes.</returns>
    IReadOnlyList<IPropertyShape> Properties { get; }

    /// <summary>
    /// Gets the constructor shape for the given type, if available.
    /// </summary>
    /// <returns>An <see cref="IConstructorShape"/> representation of the constructor.</returns>
    IConstructorShape? Constructor { get; }
}

/// <summary>
/// Provides a strongly typed shape model for a .NET object.
/// </summary>
/// <typeparam name="T">The type of .NET object.</typeparam>
[InternalImplementationsOnly]
public interface IObjectTypeShape<T> : ITypeShape<T>, IObjectTypeShape;
