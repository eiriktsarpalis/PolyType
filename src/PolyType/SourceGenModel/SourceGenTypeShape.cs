using PolyType.Abstractions;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace PolyType.SourceGenModel;

/// <summary>
/// Source generator model for type shapes.
/// </summary>
/// <typeparam name="T">The type that the shape describes.</typeparam>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public abstract class SourceGenTypeShape<T> : ITypeShape<T>
{
    /// <inheritdoc/>
    public abstract TypeShapeKind Kind { get; }

    /// <inheritdoc/>
    public required ITypeShapeProvider Provider { get; init; }

    /// <summary>
    /// Gets the factory method for creating the custom attribute provider of the type.
    /// </summary>
    public Func<SourceGenAttributeInfo[]>? AttributeFactory { get; init; }

    Type ITypeShape.Type => typeof(T);

    /// <summary>
    /// Gets the factory method for creating method shapes.
    /// </summary>
    public Func<IEnumerable<IMethodShape>>? MethodsFactory { get; init; }

    /// <summary>
    /// Gets the factory method for creating event shapes.
    /// </summary>
    public Func<IEnumerable<IEventShape>>? EventsFactory { get; init; }

    /// <summary>
    /// Gets the function that looks up associated type shapes by type.
    /// </summary>
    public Func<Type, ITypeShape?>? GetAssociatedTypeShape { get; init; }

    /// <inheritdoc/>
    public abstract object? Accept(TypeShapeVisitor visitor, object? state = null);

    /// <inheritdoc/>
    object? ITypeShape.Invoke(ITypeShapeFunc func, object? state) => func.Invoke(this, state);

    IReadOnlyList<IMethodShape> ITypeShape.Methods => field ?? CommonHelpers.ExchangeIfNull(ref field, (MethodsFactory?.Invoke()).AsReadOnlyList());

    IReadOnlyList<IEventShape> ITypeShape.Events => field ?? CommonHelpers.ExchangeIfNull(ref field, (EventsFactory?.Invoke()).AsReadOnlyList());

    IGenericCustomAttributeProvider ITypeShape.AttributeProvider => field ?? CommonHelpers.ExchangeIfNull(ref field, SourceGenCustomAttributeProvider.Create(AttributeFactory));

    ITypeShape? ITypeShape.GetAssociatedTypeShape(Type associatedType)
    {
        if (associatedType is null)
        {
            ThrowArgumentNull();
            [DoesNotReturn]
            static void ThrowArgumentNull() => throw new ArgumentNullException(nameof(associatedType));
        }

        if (associatedType.IsGenericTypeDefinition && typeof(T).GenericTypeArguments.Length != associatedType.GetTypeInfo().GenericTypeParameters.Length)
        {
            ThrowArgumentException();
            [DoesNotReturn]
            static void ThrowArgumentException() => throw new ArgumentException("Type is not a generic type definition or does not have an equal count of generic type parameters with this type shape.", nameof(associatedType));
        }

        return GetAssociatedTypeShape?.Invoke(associatedType);
    }

    private protected string DebuggerDisplay => $"{{Type = \"{typeof(T)}\", Kind = {Kind}}}";
}
