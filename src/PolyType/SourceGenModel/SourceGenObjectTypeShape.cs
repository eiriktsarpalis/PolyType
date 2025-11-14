using PolyType.Abstractions;
using System.Diagnostics;

namespace PolyType.SourceGenModel;

/// <summary>
/// Source generator model for object type shapes.
/// </summary>
/// <typeparam name="TObject">The type whose shape is described.</typeparam>
[DebuggerTypeProxy(typeof(PolyType.Debugging.ObjectTypeShapeDebugView))]
public sealed class SourceGenObjectTypeShape<TObject> : SourceGenTypeShape<TObject>, IObjectTypeShape<TObject>
{
    /// <inheritdoc/>
    public bool IsRecordType { get; init; }

    /// <inheritdoc/>
    public bool IsTupleType { get; init; }

    /// <summary>
    /// Gets the factory method for creating property shapes.
    /// </summary>
    public Func<IEnumerable<IPropertyShape>>? PropertiesFactory { get; init; }

    /// <summary>
    /// Gets the factory method for creating constructor shapes.
    /// </summary>
    public Func<IConstructorShape>? ConstructorFactory { get; init; }

    /// <inheritdoc/>
    public override TypeShapeKind Kind => TypeShapeKind.Object;

    /// <inheritdoc/>
    public override object? Accept(TypeShapeVisitor visitor, object? state = null) => visitor.VisitObject(this, state);

    IReadOnlyList<IPropertyShape> IObjectTypeShape.Properties => field ?? CommonHelpers.ExchangeIfNull(ref field, (PropertiesFactory?.Invoke()).AsReadOnlyList());

    IConstructorShape? IObjectTypeShape.Constructor
    {
        get
        {
            if (!_isConstructorResolved)
            {
                if (ConstructorFactory?.Invoke() is { } constructor)
                {
                    CommonHelpers.ExchangeIfNull(ref field, constructor);
                }

                Volatile.Write(ref _isConstructorResolved, true);
            }

            return field;
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private bool _isConstructorResolved;
}
