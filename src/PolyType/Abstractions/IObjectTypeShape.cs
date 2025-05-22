using System.Reflection;

namespace PolyType.Abstractions;

/// <summary>
/// Provides a strongly typed shape model for a .NET object.
/// </summary>
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
    /// <exception cref="NotImplementedException">Thrown on a partial shape that was not generated to include properties.</exception>
    IReadOnlyList<IPropertyShape> Properties { get; }

    /// <summary>
    /// Gets the constructor shape for the given type, if available.
    /// </summary>
    /// <returns>An <see cref="IConstructorShape"/> representation of the constructor.</returns>
    /// <exception cref="NotImplementedException">Thrown on a partial shape that was not generated to include a constructor.</exception>
    IConstructorShape? Constructor { get; }
}

/// <summary>
/// Provides a strongly typed shape model for a .NET object.
/// </summary>
/// <typeparam name="T">The type of .NET object.</typeparam>
public abstract class IObjectTypeShape<T>(ITypeShapeProvider provider) : ITypeShape<T>, IObjectTypeShape
{
    /// <inheritdoc/>
    public abstract bool IsRecordType { get; }

    /// <inheritdoc/>
    public abstract bool IsTupleType { get; }

    /// <inheritdoc/>
    public abstract IReadOnlyList<IPropertyShape> Properties { get; }

    /// <inheritdoc/>
    public abstract IConstructorShape? Constructor { get; }

    /// <inheritdoc/>
    public Type Type => typeof(T);

    /// <inheritdoc/>
    public TypeShapeKind Kind => TypeShapeKind.Object;

    /// <inheritdoc/>
    public ITypeShapeProvider Provider => provider;

    /// <inheritdoc/>
    public virtual ICustomAttributeProvider? AttributeProvider => typeof(T);

    /// <inheritdoc/>
    public object? Accept(TypeShapeVisitor visitor, object? state = null) => visitor.VisitObject(this, state);

    /// <inheritdoc/>
    public abstract ITypeShape? GetAssociatedTypeShape(Type associatedType);

    /// <inheritdoc/>
    public object? Invoke(ITypeShapeFunc func, object? state = null) => func.Invoke(this, state);
}
