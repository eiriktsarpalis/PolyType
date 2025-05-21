using PolyType.Abstractions;
using System.Reflection;
using System.Text;

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

    /// <inheritdoc/>
    public override TypeShapeKind Kind => TypeShapeKind.Object;

    /// <inheritdoc/>
    public override object? Accept(TypeShapeVisitor visitor, object? state = null) => visitor.VisitObject(this, state);

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
