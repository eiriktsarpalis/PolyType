using System.Reflection;

namespace PolyType.Abstractions;

/// <summary>
/// Provides a strongly typed shape model for a .NET object.
/// </summary>
public abstract class IObjectTypeShape(ITypeShapeProvider provider) : ITypeShape
{
    /// <inheritdoc/>
    public TypeShapeKind Kind => TypeShapeKind.Object;

    /// <inheritdoc/>
    public ITypeShapeProvider Provider => provider;

    /// <inheritdoc/>
    public virtual ICustomAttributeProvider? AttributeProvider => Type;

    /// <inheritdoc/>
    public abstract Type Type { get; }

    /// <summary>
    /// Gets a value indicating whether the current shape represents a C# record type.
    /// </summary>
    public abstract bool IsRecordType { get; }

    /// <summary>
    /// Gets a value indicating whether the current shape represents a tuple type, either <see cref="System.Tuple"/> or <see cref="System.ValueTuple"/>.
    /// </summary>
    public abstract bool IsTupleType { get; }

    /// <summary>
    /// Gets all available property/field shapes for the given type.
    /// </summary>
    /// <returns>An enumeration of all available property/field shapes.</returns>
    /// <exception cref="NotImplementedException">Thrown on a partial shape that was not generated to include properties.</exception>
    public abstract IReadOnlyList<IPropertyShape> Properties { get; }

    /// <summary>
    /// Gets the constructor shape for the given type, if available.
    /// </summary>
    /// <returns>An <see cref="IConstructorShape"/> representation of the constructor.</returns>
    /// <exception cref="NotImplementedException">Thrown on a partial shape that was not generated to include a constructor.</exception>
    public abstract IConstructorShape? Constructor { get; }

    /// <inheritdoc/>
    public abstract object? Accept(TypeShapeVisitor visitor, object? state = null);

    /// <inheritdoc/>
    public abstract ITypeShape? GetAssociatedTypeShape(Type associatedType);

    /// <inheritdoc/>
    public abstract object? Invoke(ITypeShapeFunc func, object? state = null);
}

/// <summary>
/// Provides a strongly typed shape model for a .NET object.
/// </summary>
/// <typeparam name="T">The type of .NET object.</typeparam>
public abstract class IObjectTypeShape<T>(ITypeShapeProvider provider) : IObjectTypeShape(provider), ITypeShape<T>
{
    /// <inheritdoc/>
    public override Type Type => typeof(T);

    /// <inheritdoc/>
    public override object? Accept(TypeShapeVisitor visitor, object? state = null) => visitor.VisitObject(this, state);

    /// <inheritdoc/>
    public override object? Invoke(ITypeShapeFunc func, object? state = null) => func.Invoke(this, state);
}
