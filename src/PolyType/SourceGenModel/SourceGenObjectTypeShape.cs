using PolyType.Abstractions;

namespace PolyType.SourceGenModel;

/// <summary>
/// Source generator model for object type shapes.
/// </summary>
/// <typeparam name="TObject">The type whose shape is described.</typeparam>
public sealed class SourceGenObjectTypeShape<TObject> : SourceGenTypeShape<TObject>, IObjectTypeShape<TObject>
{
    /// <summary>
    /// Gets a value indicating whether the type represents a record.
    /// </summary>
    public required bool IsRecordType { get; init; }

    /// <summary>
    /// Gets a value indicating whether the type represents a tuple.
    /// </summary>
    public required bool IsTupleType { get; init; }

    /// <summary>
    /// Gets the factory method for creating property shapes.
    /// </summary>
    public Func<IEnumerable<IPropertyShape>>? CreatePropertiesFunc { get; init; }

    /// <summary>
    /// Gets the factory method for creating constructor shapes.
    /// </summary>
    public Func<IConstructorShape>? CreateConstructorFunc { get; init; }

    /// <summary>
    /// Gets the factory for a given related type.
    /// </summary>
    public Func<Type, Func<object>?>? RelatedTypeFactories { get; init; }

    /// <inheritdoc/>
    public override TypeShapeKind Kind => TypeShapeKind.Object;

    /// <inheritdoc/>
    public override object? Accept(ITypeShapeVisitor visitor, object? state = null) => visitor.VisitObject(this, state);

    /// <inheritdoc/>
    public override Func<object>? GetRelatedTypeFactory(Type relatedType)
    {
        if (!typeof(TObject).IsGenericType)
        {
            throw new InvalidOperationException();
        }

        if (!relatedType.IsGenericTypeDefinition || relatedType.GenericTypeArguments.Length != typeof(TObject).GenericTypeArguments.Length)
        {
            throw new ArgumentException("Type is not a generic type definition or does not have an equal count of generic type parameters with this type shape.");
        }

        return this.RelatedTypeFactories?.Invoke(relatedType);
    }

    IReadOnlyList<IPropertyShape> IObjectTypeShape.Properties => _properties ??= (CreatePropertiesFunc?.Invoke()).AsReadOnlyList();
    private IReadOnlyList<IPropertyShape>? _properties;

    IConstructorShape? IObjectTypeShape.Constructor
    {
        get
        {
            if (!_isConstructorResolved)
            {
                _constructor = CreateConstructorFunc?.Invoke();
                Volatile.Write(ref _isConstructorResolved, true);
            }

            return _constructor;
        }
    }

    private bool _isConstructorResolved;
    private IConstructorShape? _constructor;
}
