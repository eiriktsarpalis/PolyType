using PolyType.Abstractions;
using System.Reflection;
using System.Text;

namespace PolyType.SourceGenModel;

/// <summary>
/// Source generator model for object type shapes.
/// </summary>
/// <typeparam name="TObject">The type whose shape is described.</typeparam>
public sealed class SourceGenObjectTypeShape<TObject>(SourceGenTypeShapeProvider provider) : IObjectTypeShape<TObject>(provider)
{
    /// <summary>
    /// Gets the factory method for creating property shapes.
    /// </summary>
    public Func<IEnumerable<IPropertyShape>>? CreatePropertiesFunc { get; init; }

    /// <summary>
    /// Gets the factory method for creating constructor shapes.
    /// </summary>
    public Func<IConstructorShape>? CreateConstructorFunc { get; init; }

    /// <summary>
    /// Gets a value indicating whether the type represents a record.
    /// </summary>
    public required bool IsRecordTypeSetter { get; init; }

    /// <inheritdoc/>
    public override bool IsRecordType => IsRecordTypeSetter;

    /// <summary>
    /// Gets a value indicating whether the type represents a tuple.
    /// </summary>
    public required bool IsTupleTypeSetter { get; init; }

    /// <inheritdoc/>
    public override bool IsTupleType => IsTupleTypeSetter;

    /// <summary>
    /// Gets the shape of an associated type, by its name.
    /// </summary>
    public Func<string, ITypeShape?>? AssociatedTypeShapes { get; init; }

    /// <inheritdoc/>
    public override IReadOnlyList<IPropertyShape> Properties => _properties ??= (CreatePropertiesFunc?.Invoke()).AsReadOnlyList();
    private IReadOnlyList<IPropertyShape>? _properties;

    /// <inheritdoc/>
    public override IConstructorShape? Constructor
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

    /// <inheritdoc/>
    public override ITypeShape? GetAssociatedTypeShape(Type associatedType) => InternalTypeShapeExtensions.GetAssociatedTypeShape(this, AssociatedTypeShapes, associatedType);
}
