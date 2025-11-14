using PolyType.Abstractions;
using System.Diagnostics;
using System.Reflection;

namespace PolyType.SourceGenModel;

/// <summary>
/// Source generator model for a property shape.
/// </summary>
/// <typeparam name="TDeclaringType">The declaring type of the property.</typeparam>
/// <typeparam name="TPropertyType">The type of the property value.</typeparam>
[DebuggerTypeProxy(typeof(PolyType.Debugging.PropertyShapeDebugView))]
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class SourceGenPropertyShape<TDeclaringType, TPropertyType> : IPropertyShape<TDeclaringType, TPropertyType>
{
    /// <inheritdoc/>
    public required int Position { get; init; }

    /// <inheritdoc/>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the custom attribute provider for the property.
    /// </summary>
    public Func<SourceGenAttributeInfo[]>? AttributeFactory { get; init; }

    /// <summary>
    /// Gets the factory function for retrieving the MemberInfo of the property.
    /// </summary>
    public Func<MemberInfo?>? MemberInfoFactory { get; init; }

    /// <inheritdoc/>
    public required IObjectTypeShape<TDeclaringType> DeclaringType { get; init; }

    /// <inheritdoc/>
    public required ITypeShape<TPropertyType> PropertyType { get; init; }

    /// <summary>
    /// Gets the getter delegate for the property.
    /// </summary>
    public Getter<TDeclaringType, TPropertyType>? Getter { get; init; }

    /// <summary>
    /// Gets the setter delegate for the property.
    /// </summary>
    public Setter<TDeclaringType, TPropertyType>? Setter { get; init; }

    /// <inheritdoc/>
    public bool IsGetterPublic { get; init; }

    /// <inheritdoc/>
    public bool IsSetterPublic { get; init; }

    /// <inheritdoc/>
    public bool IsGetterNonNullable { get; init; }

    /// <inheritdoc/>
    public bool IsSetterNonNullable { get; init; }

    /// <inheritdoc/>
    public bool IsField { get; init; }

    Getter<TDeclaringType, TPropertyType> IPropertyShape<TDeclaringType, TPropertyType>.GetGetter()
        => Getter is { } getter ? getter : throw new InvalidOperationException("Property shape does not specify a getter.");

    Setter<TDeclaringType, TPropertyType> IPropertyShape<TDeclaringType, TPropertyType>.GetSetter()
        => Setter is { } setter ? setter : throw new InvalidOperationException("Property shape does not specify a setter.");

    MemberInfo? IPropertyShape.MemberInfo => field ??= MemberInfoFactory?.Invoke();

    IGenericCustomAttributeProvider IPropertyShape.AttributeProvider => field ?? CommonHelpers.ExchangeIfNull(ref field, SourceGenCustomAttributeProvider.Create(AttributeFactory));

    ITypeShape IPropertyShape.PropertyType => PropertyType;
    IObjectTypeShape IPropertyShape.DeclaringType => DeclaringType;
    bool IPropertyShape.HasGetter => Getter is not null;
    bool IPropertyShape.HasSetter => Setter is not null;
    object? IPropertyShape.Accept(TypeShapeVisitor visitor, object? state) => visitor.VisitProperty(this, state);

    private string DebuggerDisplay => $"{{Type = \"{typeof(TPropertyType)}\", Name = \"{Name}\"}}";
}
