﻿using System.Data;
using System.Diagnostics;
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

    /// <inheritdoc/>
    public override TypeShapeKind Kind => TypeShapeKind.Object;

    /// <inheritdoc/>
    public override object? Accept(TypeShapeVisitor visitor, object? state = null) => visitor.VisitObject(this, state);

    /// <inheritdoc/>
    public IReadOnlyList<IPropertyShape> Properties => _properties ?? CommonHelpers.ExchangeIfNull(ref _properties, (CreatePropertiesFunc?.Invoke()).AsReadOnlyList());

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private IReadOnlyList<IPropertyShape>? _properties;

    /// <inheritdoc/>
    public IConstructorShape? Constructor
    {
        get
        {
            if (!_isConstructorResolved)
            {
                if (CreateConstructorFunc?.Invoke() is { } constructor)
                {
                    CommonHelpers.ExchangeIfNull(ref _constructor, constructor);
                }

                Volatile.Write(ref _isConstructorResolved, true);
            }

            return _constructor;
        }
    }

    private bool _isConstructorResolved;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private IConstructorShape? _constructor;
}
